using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;

namespace CMCS_Prototype
{
    // ===============================================================
    // CORE WPF WINDOW
    // ===============================================================

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
        }
    }

    // ===============================================================
    // BASE MVVM CLASSES & DATA MODELS
    // ===============================================================

    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class UserCredentials : ObservableObject
    {
        private string _firstName;
        public string FirstName
        {
            get => _firstName;
            set { _firstName = value; OnPropertyChanged(); }
        }

        private string _lastName;
        public string LastName
        {
            get => _lastName;
            set { _lastName = value; OnPropertyChanged(); }
        }

        private string _uniqueId;
        public string UniqueId
        {
            get => _uniqueId;
            set { _uniqueId = value; OnPropertyChanged(); }
        }
    }

    public class Lecturer
    {
        public int LecturerId { get; set; }
        public string Name { get; set; }
        public double HourlyRate { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UniqueId { get; set; }
    }

    public class ClaimDocument
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
    }

    public class Claim : ObservableObject
    {
        public int ClaimId { get; set; }
        public int LecturerId { get; set; }
        public string LecturerName { get; set; }
        public string MonthYear { get; set; }
        public double HoursWorked { get; set; }
        public double HourlyRate { get; set; }

        // Recalculates whenever HoursWorked or HourlyRate is set
        public double TotalAmount => HoursWorked * HourlyRate;

        private string _status = "Pending";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string AdditionalNotes { get; set; }

        // FIX: Use ObservableCollection for Documents to track changes internally.
        private ObservableCollection<ClaimDocument> _documents;
        public ObservableCollection<ClaimDocument> Documents
        {
            get => _documents;
            set
            {
                if (_documents != null)
                    _documents.CollectionChanged -= Documents_CollectionChanged;

                _documents = value;

                if (_documents != null)
                    _documents.CollectionChanged += Documents_CollectionChanged;

                OnPropertyChanged();
                OnPropertyChanged(nameof(DocumentCount)); // Notify UI when the collection object itself is replaced
            }
        }

        // NEW BINDABLE PROPERTY: Resolves the InvalidOperationException (binding to a read-only list's Count)
        public int DocumentCount => Documents.Count;

        // Handler to notify the UI specifically when an item is added or removed from the collection
        private void Documents_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(DocumentCount));
        }

        public Claim()
        {
            // Initialize the Documents as ObservableCollection and subscribe to changes
            _documents = new ObservableCollection<ClaimDocument>();
            _documents.CollectionChanged += Documents_CollectionChanged;
        }
    }

    // ===============================================================
    // COMMANDS AND CONVERTER
    // ===============================================================

    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                switch (status.ToLowerInvariant())
                {
                    case "approved":
                        return new SolidColorBrush(Color.FromRgb(40, 167, 69));
                    case "pending":
                        return new SolidColorBrush(Color.FromRgb(255, 193, 7));
                    case "rejected":
                        return new SolidColorBrush(Color.FromRgb(220, 53, 69));
                    default:
                        return new SolidColorBrush(Color.FromRgb(108, 117, 125));
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public RelayCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public RelayCommand(Action<T> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute((T)parameter);
    }

    // ===============================================================
    // MAIN VIEW MODEL (Core Logic)
    // ===============================================================
    public class MainViewModel : ObservableObject
    {
        // --- Navigation Property ---
        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        // --- Data & Collections ---
        public UserCredentials LoginInput { get; set; }
        public ObservableCollection<Claim> PastClaims { get; set; }
        public ObservableCollection<Claim> PendingClaims { get; set; }

        private Claim _newClaim;
        public Claim NewClaim
        {
            get => _newClaim;
            set { _newClaim = value; OnPropertyChanged(); }
        }

        // --- Commands ---
        public ICommand LecturerLoginCommand { get; private set; }
        public ICommand AdminLoginCommand { get; private set; }
        public ICommand SubmitClaimCommand { get; private set; }
        public ICommand ApproveCommand { get; private set; }
        public ICommand RejectCommand { get; private set; }
        public ICommand UploadDocumentCommand { get; private set; }

        // --- Mock Users ---
        private Lecturer currentLecturer = new Lecturer
        {
            LecturerId = 1,
            Name = "Alice Johnson",
            HourlyRate = 250.00,
            FirstName = "Alice",
            LastName = "Johnson",
            UniqueId = "1234"
        };
        private Lecturer adminUser = new Lecturer
        {
            FirstName = "Admin",
            LastName = "User",
            UniqueId = "987654"
        };
        private int nextClaimId = 1005;

        public MainViewModel()
        {
            LoginInput = new UserCredentials();

            LecturerLoginCommand = new RelayCommand(LoginAsLecturer);
            AdminLoginCommand = new RelayCommand(LoginAsAdmin);

            SubmitClaimCommand = new RelayCommand(SubmitClaim);
            ApproveCommand = new RelayCommand<Claim>(ApproveClaim);
            RejectCommand = new RelayCommand<Claim>(RejectClaim);
            UploadDocumentCommand = new RelayCommand(UploadDocument);

            // Initialize NewClaim using the parameterless constructor which sets up the Documents collection
            NewClaim = new Claim { LecturerId = currentLecturer.LecturerId, HourlyRate = currentLecturer.HourlyRate };

            InitializeMockData();
            CurrentView = new LoginView();
        }

        private bool ValidateBasicInput()
        {
            if (string.IsNullOrWhiteSpace(LoginInput.FirstName) ||
                string.IsNullOrWhiteSpace(LoginInput.LastName) ||
                string.IsNullOrWhiteSpace(LoginInput.UniqueId))
            {
                MessageBox.Show("All name, surname, and ID fields must be filled.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!int.TryParse(LoginInput.UniqueId, out _))
            {
                MessageBox.Show("Unique ID must be numerical.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void LoginAsLecturer(object parameter)
        {
            if (!ValidateBasicInput()) return;

            // LECTURER RULE: ID must be exactly 4 digits long
            if (LoginInput.UniqueId.Length == 4)
            {
                currentLecturer.FirstName = LoginInput.FirstName;
                currentLecturer.LastName = LoginInput.LastName;
                currentLecturer.Name = $"{LoginInput.FirstName} {LoginInput.LastName}";
                currentLecturer.UniqueId = LoginInput.UniqueId;

                MessageBox.Show($"Lecturer Login successful. Welcome, {currentLecturer.Name}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Navigate to the Lecturer View
                CurrentView = new LecturerView();
            }
            else
            {
                MessageBox.Show("Lecturer ID must be a numerical value with EXACTLY 4 digits (e.g., 1234).", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoginAsAdmin(object parameter)
        {
            if (!ValidateBasicInput()) return;

            // ADMIN RULE: ID must be exactly 6 digits long
            if (LoginInput.UniqueId.Length == 6)
            {
                MessageBox.Show($"Admin Login successful. Welcome, {LoginInput.FirstName}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Navigate to the Admin View
                CurrentView = new AdminView();
            }
            else
            {
                MessageBox.Show("Admin ID must be a numerical value with EXACTLY 6 digits (e.g., 987654).", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void InitializeMockData()
        {
            PastClaims = new ObservableCollection<Claim>
            {
                // Ensure Documents list in mock data is an ObservableCollection
                new Claim { ClaimId = 1001, LecturerId = 1, LecturerName = currentLecturer.Name, MonthYear = "Sep 2025", HoursWorked = 20, HourlyRate = 250, Status = "Pending", Documents = new ObservableCollection<ClaimDocument> { new ClaimDocument { FileName = "Contract.pdf" } } },
                new Claim { ClaimId = 1002, LecturerId = 1, LecturerName = currentLecturer.Name, MonthYear = "Aug 2025", HoursWorked = 19.4, HourlyRate = 250, Status = "Approved" }
            };

            PendingClaims = new ObservableCollection<Claim>
            {
                new Claim { ClaimId = 1003, LecturerId = 2, LecturerName = "John Doe", MonthYear = "Oct 2025", HoursWorked = 21, HourlyRate = 200, Status = "Pending" },
                new Claim { ClaimId = 1004, LecturerId = 3, LecturerName = "Jane Smith", MonthYear = "Oct 2025", HoursWorked = 18, HourlyRate = 300, Status = "Pending" }
            };

            var lecturerPending = PastClaims.Where(c => c.Status == "Pending" && c.LecturerId == currentLecturer.LecturerId).ToList();
            foreach (var claim in lecturerPending)
            {
                if (!PendingClaims.Any(c => c.ClaimId == claim.ClaimId))
                {
                    PendingClaims.Add(claim);
                }
            }
        }

        private void SubmitClaim(object parameter)
        {
            if (NewClaim.HoursWorked <= 0 || string.IsNullOrWhiteSpace(NewClaim.MonthYear))
            {
                MessageBox.Show("Please enter valid hours worked and month/year.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // MANDATORY DOCUMENT CHECK
            if (NewClaim.Documents.Count == 0)
            {
                MessageBox.Show("A supporting document is mandatory. Please upload at least one document before submitting the claim.", "Document Required", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newSubmission = new Claim
            {
                ClaimId = nextClaimId++,
                LecturerId = currentLecturer.LecturerId,
                LecturerName = currentLecturer.Name,
                MonthYear = NewClaim.MonthYear,
                HoursWorked = NewClaim.HoursWorked,
                HourlyRate = NewClaim.HourlyRate,
                AdditionalNotes = NewClaim.AdditionalNotes,
                Status = "Pending",
                // Copy the contents of the ObservableCollection, not the object itself
                Documents = new ObservableCollection<ClaimDocument>(NewClaim.Documents)
            };

            PastClaims.Add(newSubmission);
            PendingClaims.Add(newSubmission);

            // Reset the form by creating a new instance
            NewClaim = new Claim { LecturerId = currentLecturer.LecturerId, HourlyRate = currentLecturer.HourlyRate };
            OnPropertyChanged(nameof(NewClaim));

            MessageBox.Show($"Claim {newSubmission.ClaimId} submitted successfully.\nTotal Amount: R{newSubmission.TotalAmount:N2}", "Submission Successful");
        }

        private void UploadDocument(object parameter)
        {
            try
            {
                if (NewClaim.Documents.Count >= 5)
                {
                    MessageBox.Show("Maximum of 5 documents allowed per claim.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Claim Documents (*.pdf;*.docx;*.xlsx)|*.pdf;*.docx;*.xlsx";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        FileInfo fileInfo = new FileInfo(filePath);

                        // File size limit check (5MB)
                        if (fileInfo.Length > 5242880) // 5MB in bytes
                        {
                            MessageBox.Show($"File '{Path.GetFileName(filePath)}' exceeds the 5MB limit and was not added.", "File Too Large", MessageBoxButton.OK, MessageBoxImage.Error);
                            continue;
                        }

                        NewClaim.Documents.Add(new ClaimDocument
                        {
                            FileName = Path.GetFileName(filePath),
                            FilePath = filePath
                        });
                    }

                    MessageBox.Show($"{NewClaim.Documents.Count} document(s) attached and ready for submission.", "Document Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during document upload: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApproveClaim(Claim claimToApprove)
        {
            UpdateClaimStatus(claimToApprove, "Approved", "approved successfully.");
        }

        private void RejectClaim(Claim claimToReject)
        {
            UpdateClaimStatus(claimToReject, "Rejected", "rejected.");
        }

        private void UpdateClaimStatus(Claim claim, string newStatus, string message)
        {
            if (claim == null) return;

            claim.Status = newStatus;

            var pendingItem = PendingClaims.FirstOrDefault(c => c.ClaimId == claim.ClaimId);
            if (pendingItem != null)
            {
                PendingClaims.Remove(pendingItem);
            }

            var pastItem = PastClaims.FirstOrDefault(c => c.ClaimId == claim.ClaimId);
            if (pastItem != null)
            {
                pastItem.Status = newStatus;
            }

            MessageBox.Show($"Claim {claim.ClaimId} was {message}", "Action Complete");
        }
    }
}