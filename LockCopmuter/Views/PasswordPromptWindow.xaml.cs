using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace LockCopmuter.Views;

public partial class PasswordPromptWindow : Window, INotifyPropertyChanged
{
    private string _errorText = "";
    public string ErrorText
    {
        get => _errorText;
        set { _errorText = value; OnPropertyChanged(); }
    }

    private Visibility _errorVisibility = Visibility.Collapsed;
    public Visibility ErrorVisibility
    {
        get => _errorVisibility;
        set { _errorVisibility = value; OnPropertyChanged(); }
    }

    private bool _isPasswordVisible;
    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set
        {
            _isPasswordVisible = value;
            OnPropertyChanged();

            // להעביר פוקוס לשדה המתאים
            Dispatcher.InvokeAsync(() =>
            {
                if (IsPasswordVisible)
                {
                    PwdText.Focus();
                    PwdText.CaretIndex = PwdText.Text.Length;
                }
                else
                {
                    PwdBox.Focus();
                }
            });
        }
    }

    private string _visiblePassword = "";
    public string VisiblePassword
    {
        get => _visiblePassword;
        set
        {
            _visiblePassword = value ?? "";
            OnPropertyChanged();

            // סנכרון TextBox -> PasswordBox
            if (PwdBox.Password != _visiblePassword)
                PwdBox.Password = _visiblePassword;
        }
    }

    // תמיד מחזיר את הסיסמה הנכונה, גם אם מוצגת
    public string Password => IsPasswordVisible ? (VisiblePassword ?? "") : (PwdBox.Password ?? "");

    public PasswordPromptWindow()
    {
        InitializeComponent();
        DataContext = this;

        Loaded += (_, __) => PwdBox.Focus();

        // סנכרון PasswordBox -> TextBox
        PwdBox.PasswordChanged += (_, __) =>
        {
            if (VisiblePassword != PwdBox.Password)
                VisiblePassword = PwdBox.Password;
        };

        // Enter מאשר גם ב-PwdBox וגם ב-PwdText
        PwdBox.KeyDown += OnEnter;
        PwdText.KeyDown += OnEnter;
    }

    private void OnEnter(object? sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            Ok_Click(this, new RoutedEventArgs());
        }
    }


    public void ShowError(string message)
    {
        ErrorText = message;
        ErrorVisibility = Visibility.Visible;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
