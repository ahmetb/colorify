using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Navigation;
using Facebook;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using Microsoft.Xna.Framework.Media;

namespace Colorify
{
    public partial class SharePage : PhoneApplicationPage
    {
        private readonly string fbApiAppId = "219204751460459";
        private readonly string[] _extendedPermissions = new[] { "publish_stream" };

        public static readonly string FACEBOOK_SETTING_KEY = "fbOAuthToken";
        public static readonly string TWITTER_USERNAME_KEY = "twUsername";
        public static readonly string TWITTER_PASSWORD_KEY = "twPassword";  

        private string filename;
        
        private FacebookClient _fbClient;
        private SocialNetwork target = SocialNetwork.NONE;
        
        private readonly string shareText = "Click here to share a text with picture.";
        private int uploadInProgres = 0;

        private delegate void UploadFinishedDelegate(bool result, SocialNetwork network);
        private event UploadFinishedDelegate UploadFinished;

        ApplicationBarMenuItem fbitem = new ApplicationBarMenuItem("Logout Facebook");
        ApplicationBarMenuItem twitem = new ApplicationBarMenuItem("Logout Twitter");

        public SharePage()
        {
            InitializeComponent();
            titleBar.Text = shareText;
            
            UploadFinished += new UploadFinishedDelegate(SharePage_UploadFinished);
            CheckLogoutButtons();
        }
        
        private void CheckLogoutButtons()
        {
            //Dispatcher.BeginInvoke(() =>
            {
                // logout functionality
                if (SettingsProvider.Get(FACEBOOK_SETTING_KEY) != null)
                {
                    if (!ApplicationBar.MenuItems.Contains(fbitem))
                    {
                        fbitem.Click += facebookLogout_click;
                        ApplicationBar.MenuItems.Add(fbitem);
                    }
                }
                else
                {
                    fbitem.Click -= facebookLogout_click;
                    ApplicationBar.MenuItems.Remove(fbitem);
                }

                if (SettingsProvider.Get(TWITTER_USERNAME_KEY) != null)
                {
                    if (!ApplicationBar.MenuItems.Contains(twitem))
                    {
                        twitem.Click += twitterLogout_click;
                        ApplicationBar.MenuItems.Add(twitem);
                    }
                    
                }
                else
                {
                    twitem.Click -= twitterLogout_click;
                    ApplicationBar.MenuItems.Remove(twitem);
                }
            }
            //);
        }

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            bool cancel = false;

            if (browserAuth.Visibility == Visibility.Visible)
            {
                browserAuth.Visibility = Visibility.Collapsed;
                cancel = true;
            }

            // cancel the navigation?
            e.Cancel = cancel;
        }

        private void twitterLogout_click(object sender, EventArgs e)
        {
            SettingsProvider.Delete(TWITTER_USERNAME_KEY);
            SettingsProvider.Delete(TWITTER_PASSWORD_KEY);
            frmTwitterUsername.Text = "";
            frmTwitterPassword.Password = "";
            if(target == SocialNetwork.TWITTER)
            {
                target = SocialNetwork.NONE;
                listBox1.SelectedIndex = -1;
            }
            CheckLogoutButtons();
        }

       
        private void facebookLogout_click(object sender, EventArgs e)
        {
            bool b = SettingsProvider.Delete(FACEBOOK_SETTING_KEY);
            if (_fbClient != null && _fbClient.AccessToken != null)
            {
                _fbClient.AccessToken = null;
            }
            if (target == SocialNetwork.FACEBOOK)
            {
                target = SocialNetwork.NONE;
                listBox1.SelectedIndex = -1;
            }

            browserAuth.Visibility = Visibility.Collapsed;
            browserAuth.Navigate(new Uri("http://m.facebook.com/logout.php?confirm=1"));
            CheckLogoutButtons();
        }

        void SharePage_UploadFinished(bool result, SharePage.SocialNetwork network)
        {
            Interlocked.Exchange(ref uploadInProgres, 0);
            Dispatcher.BeginInvoke( () => { loading.IsIndeterminate = false; } );
        }

        private enum SocialNetwork
        {
            NONE,
            EMAIL,
            FACEBOOK,
            TWITTER
        }

      
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Get a dictionary of query string keys and values.
            IDictionary<string, string> queryStrings = this.NavigationContext.QueryString;

            // Ensure that there is at least one key in the query string, and check whether the "token" key is present.
            if (queryStrings.ContainsKey("filename"))
            {
                filename = queryStrings["filename"];
                //use this filename to upload the photo. You can use media library to get it.
            }
        }

        private void ApplicationBarCancelButton_Click(object sender, EventArgs e)
        {
            if(browserAuth.Visibility == Visibility.Visible)
            {
                browserAuth.Visibility = Visibility.Collapsed;
            }
            else
            {
                NavigationService.GoBack();
            }
        }

        private void titleBar_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (titleBar.Text.Equals(shareText))
                titleBar.Text = "";
        }

        
        
        private void SendAsEmail()
        {
            string body = "";
            if (!titleBar.Text.Equals(shareText))
            {
                 body = titleBar.Text;
            }

            MessageBox.Show("Please don't forget to add your image as attachment. This version of Windows Phone SDK does not allow us to add images as attachment automatically.", "Warning", MessageBoxButton.OK);
            App.SendEmail("", "Colorified Picture", body);
        }

        private void LoginToFacebook()
        {
            loading.IsIndeterminate = true;
            browserAuth.IsScriptEnabled = true;

            var loginParameters = new Dictionary<string, object>
                                      {
                                          { "response_type", "token" },
                                          { "display", "touch" } // by default for wp7 builds only (in Facebook.dll), display is set to touch.
                                      };

            

            var navigateUrl = FacebookOAuthClient.GetLoginUrl(fbApiAppId, null, _extendedPermissions, loginParameters);
            browserAuth.Navigate(navigateUrl);
            browserAuth.Visibility = Visibility.Visible;
        }

        private void FacebookLoginBrowser_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            FacebookOAuthResult oauthResult;
            if (FacebookOAuthResult.TryParse(e.Uri, out oauthResult))
            {
                if (oauthResult.IsSuccess)
                {
                    _fbClient = new FacebookClient(oauthResult.AccessToken);
                    _fbClient.PostCompleted += new EventHandler<FacebookApiEventArgs>(fbApp_PostCompleted);
                    SettingsProvider.Set(FACEBOOK_SETTING_KEY, oauthResult.AccessToken);
                    CheckLogoutButtons();
                }
                browserAuth.Visibility = Visibility.Collapsed;
            }
            else
            {
                string fbLogoutDoc = browserAuth.SaveToString(); 
                Regex regex = new Regex("\\<A href=\\\"(.*)\\\".*data-sigil=\\\"logout\\\""); 
                MatchCollection matches = regex.Matches(fbLogoutDoc); 
                
                if (matches.Count > 0)
                {
                    string finalLogout = string.Format("http://m.facebook.com{0}", matches[0].Groups[1].ToString());
                    browserAuth.Navigate(new Uri(finalLogout));
                }
            }

            loading.IsIndeterminate = false;
        }

        private void listBox1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            switch (listBox1.SelectedIndex)
            {
                case 0: //Facebook
                    target = SocialNetwork.FACEBOOK;
                    twitterGrid.Visibility = Visibility.Collapsed;

                    // use existing oauth token
                    if (string.IsNullOrEmpty(SettingsProvider.Get(FACEBOOK_SETTING_KEY)))
                    {
                        LoginToFacebook(); // opens browser bla bla
                    } 
                    else
                    {
                        _fbClient = new FacebookClient(SettingsProvider.Get(FACEBOOK_SETTING_KEY));
                        _fbClient.PostCompleted += new EventHandler<FacebookApiEventArgs>(fbApp_PostCompleted);
                    }
                    break;
                case 1:
                    target = SocialNetwork.TWITTER;

                    // use existing password
                    if (!string.IsNullOrEmpty(SettingsProvider.Get(TWITTER_USERNAME_KEY)))
                    {
                        frmTwitterUsername.Text = SettingsProvider.Get(TWITTER_USERNAME_KEY);
                        frmTwitterRemember.IsChecked = true;
                    }
                    if (!string.IsNullOrEmpty(SettingsProvider.Get(TWITTER_PASSWORD_KEY)))
                    {
                        frmTwitterPassword.Password = SettingsProvider.Get(TWITTER_PASSWORD_KEY);
                    }

                    twitterGrid.Visibility = Visibility.Visible;
                    break;
                case 2:
                    target = SocialNetwork.EMAIL;
                    twitterGrid.Visibility = Visibility.Collapsed;
                    SendAsEmail();
                    break;
                default:
                    target = SocialNetwork.NONE;
                    break;
            }
        }

        private void ApplicationBarOkButton_Click(object sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(ref uploadInProgres, 1, 0) == 0)
            {
                this.Focus();
                switch (target)
                {
                    case SocialNetwork.FACEBOOK:
                        ShareToFacebook();
                        break;
                    case SocialNetwork.EMAIL:
                        SendAsEmail();
                        Interlocked.Exchange(ref uploadInProgres, 0);
                        break;
                    case SocialNetwork.TWITTER:
                        SaveTwitterCredentialsIfRemember();
                        ShareToTwitter();
                        break;

                    default:
                        Interlocked.Exchange(ref uploadInProgres, 0);
                        break;
                }
            }
        }

        private void SaveTwitterCredentialsIfRemember()
        {
            if (frmTwitterRemember.IsChecked == true)
            {
                SettingsProvider.Set(TWITTER_USERNAME_KEY, frmTwitterUsername.Text);
                SettingsProvider.Set(TWITTER_PASSWORD_KEY, frmTwitterPassword.Password);
            }
            else
            {
                // if not checked remove username and password
                SettingsProvider.Delete(TWITTER_USERNAME_KEY);
                SettingsProvider.Delete(TWITTER_PASSWORD_KEY);
            }
            CheckLogoutButtons();
        }

        private void ShareToTwitter()
        {
            string twitterUsername = frmTwitterUsername.Text;
            if (string.IsNullOrEmpty(twitterUsername)) { Interlocked.Exchange(ref uploadInProgres, 0); frmTwitterUsername.Focus(); return; }
            string twitterPassword = frmTwitterPassword.Password;
            if (string.IsNullOrEmpty(twitterPassword)) { Interlocked.Exchange(ref uploadInProgres, 0); frmTwitterPassword.Focus(); return; }

            string endpoint = "http://twitpic.com/api/uploadAndPost";

            loading.IsIndeterminate = true;
            TwitpicClient twitpic = new TwitpicClient();
            string text = null;
            if (!titleBar.Text.Equals(shareText))
            {
                text = titleBar.Text;
            }

            ThreadStart threadStart = new ThreadStart(() =>
            {
                bool uploaded = false;
                try
                {
                    byte[] contents = GetLastSavedPictureContents();
                    uploaded = twitpic.UploadPhoto(contents, text, twitterUsername, twitterPassword);
                }
                catch(Exception ex)
                {
                   
                }

                UploadFinished(uploaded, SocialNetwork.TWITTER);
                Dispatcher.BeginInvoke( () => {
                    loading.IsIndeterminate = false;
                    if (uploaded)
                    {
                        MessageBox.Show("You have succesfully posted photo to your Twitter stream.", "Tip", MessageBoxButton.OK);
                    } 
                    else
                    {
                        MessageBox.Show("Failed to send photo to Twitpic. Check you username/password or please try again later.", "Error", MessageBoxButton.OK);
                    }
                });
            });

            Thread uploadThread = new Thread(threadStart);
            uploadThread.Start();
        }

        private void ShareToFacebook()
        {
            if( _fbClient == null || _fbClient.AccessToken == null)
            {
                LoginToFacebook();
                Interlocked.Exchange(ref uploadInProgres, 0);
                return;
            }

            loading.IsIndeterminate = true;
            byte[] contents = null;
            try
            {
                contents = GetLastSavedPictureContents();
                if (contents == null || contents.Length == 0)
                {
                    UploadFinished(false, SocialNetwork.FACEBOOK);
                    MessageBox.Show("Could not load last saved picture. Sorry.", "Error", MessageBoxButton.OK);
                    return;
                }
            }
            catch (Exception e)
            {
                UploadFinished(false, SocialNetwork.FACEBOOK);
                MessageBox.Show("Error occurred during reading saved image.", "Error", MessageBoxButton.OK);
                return;
            }

            var mediaObject = new FacebookMediaObject
            {
                FileName = filename,
                ContentType = "image/jpeg",
            };
            mediaObject.SetValue(contents);

            var parameters = new Dictionary<string, object>();
            parameters["access_token"] = _fbClient.AccessToken;

            parameters["source"] = mediaObject;
            if (titleBar.Text != null && !titleBar.Text.Equals(shareText))
            {
                parameters["message"] = titleBar.Text;
            }
            
            _fbClient.PostAsync("me/photos", parameters);
        }

        private byte[] GetLastSavedPictureContents()
        {
            Picture picture = null;
            MediaLibrary media = new MediaLibrary();
            foreach (var savedPicture in media.SavedPictures)
            {
                if (savedPicture.Name == filename)
                {
                    picture = savedPicture;
                    break;
                }
            }

            if (picture == null)
                return null;
            
            Stream imageStream = picture.GetImage();

            byte[] contents = new byte[imageStream.Length];
            int read = imageStream.Read(contents, 0, (int)contents.Length); // safe cast

            return contents;
        }

        void fbApp_PostCompleted(object sender, FacebookApiEventArgs e)
        {
            bool fail = e.Cancelled || e.Error != null;
            //if (fail) SettingsProvider.Delete(FACEBOOK_SETTING_KEY);  // In the case of OAuth key is invalidated or access revoked!
            
            UploadFinished(!fail, SocialNetwork.FACEBOOK);
            
            Dispatcher.BeginInvoke(() =>
            {
                loading.IsIndeterminate = false;
                if (fail)
                {
                    MessageBox.Show("Photo upload failed. Please try again later.", "Error", MessageBoxButton.OK);
                }
                else
                {
                    MessageBox.Show("You have succesfully posted photo to your Facebook profile.", "Tip", MessageBoxButton.OK);
                }
                
            });
        }

        private void UsernamePassword_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.Focus();
                ApplicationBarOkButton_Click(sender,e);
            }
        }

        private void ApplicationBarRateMenuItem_Click(object sender, EventArgs e)
        {
            MarketplaceReviewTask mpt=new MarketplaceReviewTask();
            mpt.Show();

        }

        private void ApplicationBarFeedbackMenuItem_Click(object sender, EventArgs e)
        {
            App.SendEmail(App.COLORIFY_EMAIL, "Feedback", "");
        }

        private void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplicationLicense.CheckTrialMode(adControl);
        }

        private void adControl_ErrorOccurred(object sender, Microsoft.Advertising.AdErrorEventArgs e)
        {
            adControl.Refresh();
        }
    }
}