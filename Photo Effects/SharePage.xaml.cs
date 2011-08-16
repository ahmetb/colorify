using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Facebook;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using Microsoft.Xna.Framework.Media;

namespace Photo_Colorify
{
    public partial class SharePage : PhoneApplicationPage
    {
        private readonly string fbApiAppId = "219204751460459";
        private readonly string[] _extendedPermissions = new[] { "publish_stream" };

        SettingsProvider settings;

        private readonly string FACEBOOK_SETTING_KEY = "fbOAuthToken";
        private readonly string TWITTER_USERNAME_KEY = "twUsername";
        private readonly string TWITTER_PASSWORD_KEY = "twPassword";

        private string filename;
        
        private FacebookClient _fbClient;
        private SocialNetwork target = SocialNetwork.NONE;
        
        private readonly string shareText = "Click to share a text with picture.";
        private bool uploadInProgres = false;

        private delegate void UploadFinishedDelegate(bool result, SocialNetwork network);
        private event UploadFinishedDelegate UploadFinished;

        public SharePage()
        {
            InitializeComponent();
            titleBar.Text = shareText;
            settings = new SettingsProvider();

            UploadFinished += new UploadFinishedDelegate(SharePage_UploadFinished);    
        }

        void SharePage_UploadFinished(bool result, SharePage.SocialNetwork network)
        {
            uploadInProgres = false;
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
            EmailComposeTask emailTask = new EmailComposeTask();
            if (titleBar.Text != null && !titleBar.Text.Equals(shareText))
            {
                emailTask.Body = titleBar.Text;
            }
            emailTask.Show();
        }

        private void LoginToFacebook()
        {
            browserAuth.IsScriptEnabled = true;
            browserAuth.Navigated += FacebookLoginBrowser_Navigated;

            var loginParameters = new Dictionary<string, object>
                                      {
                                          { "response_type", "token" },
                                          { "display", "touch" } // by default for wp7 builds only (in Facebook.dll), display is set to touch.
                                      };

            loading.IsIndeterminate = true;

            var navigateUrl = FacebookOAuthClient.GetLoginUrl(fbApiAppId, null, _extendedPermissions, loginParameters);

            browserAuth.Navigate(navigateUrl);
        }

        private void FacebookLoginBrowser_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            //FacebookOAuthResult.TryParse()
            FacebookOAuthResult oauthResult;
            if (FacebookOAuthResult.TryParse(e.Uri, out oauthResult))
            {
                if (oauthResult.IsSuccess)
                {
                    _fbClient = new FacebookClient(oauthResult.AccessToken);
                    settings.Set(FACEBOOK_SETTING_KEY, oauthResult.AccessToken);
                }
                browserAuth.Visibility = Visibility.Collapsed;
                //TitlePanel.Visibility = System.Windows.Visibility.Visible;
                //ContentPanel.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                //TitlePanel.Visibility = System.Windows.Visibility.Collapsed;
                //ContentPanel.Visibility = System.Windows.Visibility.Collapsed;
                browserAuth.Visibility = Visibility.Visible;
            }

            loading.IsIndeterminate = false;
        }

        private void listBox1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            listBox1_SelectionChanged(sender, null);
        }

        private void listBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (listBox1.SelectedIndex)
            {
                case 0: //Facebook
                    target = SocialNetwork.FACEBOOK;
                    twitterGrid.Visibility = Visibility.Collapsed;

                    // use existing oauth token
                    if (string.IsNullOrEmpty(settings.Get(FACEBOOK_SETTING_KEY)))
                    {
                        LoginToFacebook(); // opens browser bla bla
                    } 
                    else
                    {
                        _fbClient = new FacebookClient(settings.Get(FACEBOOK_SETTING_KEY));
                    }
                    break;
                case 1:
                    target = SocialNetwork.TWITTER;

                    // use existing password
                    if (!string.IsNullOrEmpty(settings.Get(TWITTER_USERNAME_KEY)))
                    {
                        frmTwitterUsername.Text = settings.Get(TWITTER_USERNAME_KEY);
                        frmTwitterRemember.IsChecked = true;
                    }
                    if (!string.IsNullOrEmpty(settings.Get(TWITTER_PASSWORD_KEY)))
                    {
                        frmTwitterPassword.Password = settings.Get(TWITTER_PASSWORD_KEY);
                    }

                    twitterGrid.Visibility = Visibility.Visible;
                    break;
                case 2:
                    target = SocialNetwork.EMAIL;
                    twitterGrid.Visibility = Visibility.Collapsed;
                    break;
                default:
                    target = SocialNetwork.NONE;
                    break;
            }
        }

        private void ApplicationBarOkButton_Click(object sender, EventArgs e)
        {
            if(uploadInProgres)
                return;

            switch(target)
            {
                case SocialNetwork.FACEBOOK:
                    uploadInProgres = true;
                    ShareToFacebook();
                    break;
                case SocialNetwork.EMAIL:
                    SendAsEmail();
                    break;
                case SocialNetwork.TWITTER:
                    uploadInProgres = true;
                    SaveTwitterCredentialsIfRemember();
                    ShareToTwitter();
                    break;
            }
        }

        private void SaveTwitterCredentialsIfRemember()
        {
            if (frmTwitterRemember.IsChecked == true)
            {
                settings.Set(TWITTER_USERNAME_KEY, frmTwitterUsername.Text);
                settings.Set(TWITTER_PASSWORD_KEY, frmTwitterPassword.Password);
            }
            else
            {
                // if not checked remove username and password
                settings.Delete(TWITTER_USERNAME_KEY);
                settings.Delete(TWITTER_PASSWORD_KEY);
            }
        }

        private void ShareToTwitter()
        {
            string twitterUsername = frmTwitterUsername.Text;
            if (string.IsNullOrEmpty(twitterUsername)) { uploadInProgres = false; frmTwitterUsername.Focus(); return; }
            string twitterPassword = frmTwitterPassword.Password;
            if (string.IsNullOrEmpty(twitterPassword)) { uploadInProgres = false; frmTwitterPassword.Focus(); return; }

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
                    uploaded = twitpic.UploadPhoto(GetLastSavedPictureContents(), text, twitterUsername, twitterPassword);
                }
               catch(Exception ex)
               {
                   
               }

                UploadFinished(uploaded, SocialNetwork.FACEBOOK);
                Dispatcher.BeginInvoke( () => {
                loading.IsIndeterminate = false;
                if (uploaded)
                {
                    MessageBox.Show("You have succesfully posted photo to your Twitter stream.");
                } 
                else
                {
                    MessageBox.Show("Failed to send photo to Twitpic. Check you username/password or please try again later.");
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
                uploadInProgres = false;
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
                    MessageBox.Show("Could not load last saved picture. Sorry.");
                    browserAuth.Navigated -= FacebookLoginBrowser_Navigated;
                    return;
                }
            }
            catch (Exception e)
            {
                UploadFinished(false, SocialNetwork.FACEBOOK);
                MessageBox.Show("Error occurred during reading saved image.");
                browserAuth.Navigated -= FacebookLoginBrowser_Navigated;
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

            _fbClient.PostCompleted += new EventHandler<FacebookApiEventArgs>(fbApp_PostCompleted);
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


            Stream imageStream = picture.GetImage();

            byte[] contents = new byte[imageStream.Length];
            int read = imageStream.Read(contents, 0, (int)contents.Length); // safe cast

            return contents;
        }

        void fbApp_PostCompleted(object sender, FacebookApiEventArgs e)
        {
            bool fail = e.Cancelled || e.Error != null;
            if (fail) settings.Delete(FACEBOOK_SETTING_KEY);  // In the case of OAuth key is invalidated or access revoked!
            
            UploadFinished(!fail, SocialNetwork.FACEBOOK);
            
            Dispatcher.BeginInvoke(() =>
            {
                loading.IsIndeterminate = false;
                if (fail)
                {
                    MessageBox.Show("Photo upload failed. Please try again later.");
                }
                else
                {
                    MessageBox.Show("You have succesfully posted photo to your Facebook profile.");
                }
                
                browserAuth.Navigated -= FacebookLoginBrowser_Navigated;
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

     

    }
}