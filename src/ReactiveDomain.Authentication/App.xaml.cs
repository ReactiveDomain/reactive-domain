using System.Windows;

namespace ReactiveDomain.Authentication
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var main = new Main();
            LogIn(main);
        }
        public void LogIn(Window window)
        {
            //await LogIn();
            window.Show();
        }
        //public async Task<LoginResult> LogIn()
        //{
        //    var loginResult = await UserValidation.DisplayLoginUI("Elbe.Authentication", "", "http://localhost/elbe");
        //    return loginResult;
        //}
    }
}
