using IdentityModel.OidcClient.Browser;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ReactiveDomain.Authentication
{
    public class WebViewWrapper : IBrowser
    {
        private string formData;
        private BrowserOptions _options = null;


        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
            _options = options;
            var mainWindow = Application.Current?.MainWindow;
            Window owner = mainWindow?.IsLoaded == true ? mainWindow : null;

            var window = new LoginHost()
            {
                Width = 800,
                Height = 625,
                Title = "Login",
                ResizeMode = ResizeMode.NoResize,
                Owner = owner,
                Topmost = true
            };


            var webBrowser = window.webView;


            var signal = new SemaphoreSlim(0, 1);

            var result = new BrowserResult()
            {
                ResultType = BrowserResultType.UserCancel
            };

            webBrowser.NavigationStarting += (s, e) =>
            {
                if (BrowserIsNavigatingToRedirectUri(new Uri(e.Uri)))
                {
                    e.Cancel = true;

                    GetForm(window);

                }
            };

            window.Closing += (s, e) =>
            {
                signal.Release();
            };
            if (webBrowser.CoreWebView2 == null)
            {
                webBrowser.Source = new Uri(_options.StartUrl);
            }
            else
            {
                webBrowser.CoreWebView2.Navigate(_options.StartUrl);
            }
            window.ShowDialog();



            result = new BrowserResult()
            {
                ResultType = BrowserResultType.Success,
                Response = formData
            };


            await signal.WaitAsync();

            return result;
        }
        private async void GetForm(LoginHost window)
        {

            var js = @"
        function formToURL(){
            var formElement = document.getElementsByTagName(""form"")[0],
            inputElements = formElement.getElementsByTagName(""input""),
            resultUrl = ""?"";
            for(var i = 0; i < inputElements.length; i++){
                var inputElement = inputElements[i];
                    resultUrl += inputElement.getAttribute(""name"") + ""="";
                    resultUrl += inputElement.getAttribute(""value"") + ""&"";
            }
            
            return resultUrl.substring(0, resultUrl.length - 1);
        };
        formToURL();
        ";
            var response = await window.webView.ExecuteScriptAsync(js);
            formData = response;
            window.Close();
        }


        private bool BrowserIsNavigatingToRedirectUri(Uri uri)
        {
            return uri.AbsoluteUri.StartsWith(_options.EndUrl);
        }

    }
}
