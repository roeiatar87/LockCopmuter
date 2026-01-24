using System.Windows;
using LockCopmuter.ViewModels;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace LockCopmuter.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void UpdatePassword_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.TrySetPassword(NewPwd.Password, ConfirmPwd.Password);

            NewPwd.Clear();
            ConfirmPwd.Clear();

            MessageBox.Show("הסיסמה עודכנה בהצלחה.", "LockCopmuter",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
