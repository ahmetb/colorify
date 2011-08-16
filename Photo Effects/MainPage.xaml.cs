using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace Photo_Colorify
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        ImageManipulator imageMan;
        Brush brush = new Brush();
        private PhotoAction action = PhotoAction.Color;
        private string DEFAULT_WARNING_TEXT = "PLEASE WAIT TO SELECT A PICTURE FIRST...";
        private PhotoChooserTask photoChooserTask;
        private ApplicationBarIconButton changebutton;
        SettingsProvider settings;

        public MainPage()
        {
            InitializeComponent();
            settings = new SettingsProvider();
        }      

        private void ContentPanel_Loaded(object sender, RoutedEventArgs e)
        {
            TileImage.Source = new BitmapImage(new Uri("/Images/StripeColorful.png", UriKind.RelativeOrAbsolute));
            if (imageMan==null)
                ApplicationBarIconLoadButton_Click(sender, e); // trigger manually
        }

       void photoChooserTask_Completed(object sender, PhotoResult e)
        {
            if(e.ChosenPhoto!=null)
            {
                iamgeProgressBar.IsIndeterminate = true;
                warning.Text = "LOADING GRAYSCALE PICTURE...";
                image.Visibility = Visibility.Collapsed;
                Stream choosenPhoto = e.ChosenPhoto;

                if (action != PhotoAction.Color)
                {
                    action = PhotoAction.Gray;
                    foreach (ApplicationBarIconButton button in this.ApplicationBar.Buttons)
                    {
                        if (button.Text.Equals("Color"))
                        {
                            changebutton = button;
                            ApplicationBarIconColorButton_Click(changebutton, null);
                            break;
                        }
                    }
                }

                imageMan = new ImageManipulator(choosenPhoto, image);
                ThreadStart loadStart = new ThreadStart( () => {

                    DateTime Start = DateTime.Now;
                    imageMan.convertToBlackWhite();
                    int timeSpend = (int)(DateTime.Now - Start).TotalMilliseconds;
                    
                    if( timeSpend < 2000)
                    {
                        Thread.Sleep(2000 - timeSpend);
                    }

                    Dispatcher.BeginInvoke( () => {
                        image.Height = (image.Width*imageMan.originalImage.PixelHeight)/imageMan.originalImage.PixelWidth;
                        image.Source = imageMan.finalImage;
                        image.Visibility = Visibility.Visible;
                        iamgeProgressBar.IsIndeterminate = false;
                        warning.Text = "";

                        if (action != PhotoAction.Color && changebutton!=null)
                            ApplicationBarIconColorButton_Click(changebutton, null);
                    });

                    System.GC.Collect();
                } );

                Thread loadImage=new Thread(loadStart);
                loadImage.Start();
            }
            else if(imageMan==null)
            {
                warning.Text = DEFAULT_WARNING_TEXT;
                ((PhotoChooserTask)sender).Show();
            }
        }

        private void gray_Click(object sender, RoutedEventArgs e)
        {
            // The image will be read from isolated storage into the following byte array
            try
            {
                byte[] data;
                using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    var files = isf.GetDirectoryNames();
                    using (IsolatedStorageFileStream isfs = isf.OpenFile("WP7Logo.png", FileMode.Open, FileAccess.Read))
                    {
                        data = new byte[isfs.Length];
                        isfs.Read(data, 0, data.Length);
                        isfs.Close();
                    }
                }

                MemoryStream ms = new MemoryStream(data);
                BitmapImage bi = new BitmapImage();
                bi.SetSource(ms);
                image.Height = bi.PixelHeight;
                image.Width = bi.PixelWidth;
                image.Source = bi;
            }
            catch (Exception ex)
            {
                //do nothing
            }

            action = PhotoAction.Gray;
            image.Source = imageMan.finalImage;
        }

        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            image_MouseMove(sender, e);
        }

        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            if (imageMan!=null)
            {
                UIElement uiel = (UIElement)sender;
                Point p = e.GetPosition(uiel);
                WriteableBitmap im = image.Source as WriteableBitmap;

                int xClick = (int)(im.PixelWidth * (p.X / image.ActualWidth));
                int yClick = (int)(im.PixelHeight * (p.Y / image.ActualHeight));
                int cRadius = (int) ((im.PixelWidth * brush.Size) / image.Width);

                imageMan.MofifyImage(action, xClick, yClick, cRadius);
                image.Source = imageMan.finalImage;
            }
        }

        private void image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Get a dictionary of query string keys and values.
            IDictionary<string, string> queryStrings = this.NavigationContext.QueryString;

            // Ensure that there is at least one key in the query string, and check whether the "token" key is present.
            if (queryStrings.ContainsKey("token"))
            {
                // Retrieve the picture from the media library using the token passed to the application.
                warning.Text = "";
                imageMan = new ImageManipulator(queryStrings["token"], image);
                image.Source = imageMan.finalImage;
               }
        }

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            ApplicationBarIconLoadButton_Click(null, null);

            // cancel the navigation
            e.Cancel = true;
        }



        private void ApplicationBarIconShareButton_Click(object sender, EventArgs e)
        {
            if (imageMan == null)
                return;

            if(imageMan.modified || string.IsNullOrEmpty(imageMan.lastSavedFileName))
            {
                if(!SaveImageToLibrary(false))
                    return;
            }

            
            NavigationService.Navigate(new Uri("/SharePage.xaml?filename="+imageMan.lastSavedFileName, UriKind.Relative));
        }

        private bool SaveImageToLibrary(bool showsuccess)
        {
            if (imageMan == null || !imageMan.modified)
                return false;
            
            try
            {
                imageMan.SaveToMediaLibrary();
                if (showsuccess)
                    MessageBox.Show("Image saved. You can share it with friends and family :)", "Information", MessageBoxButton.OK);
                return true;
            }
            catch (Exception ex)
            {
                string msg = string.Format("Image saved failed! Please try again. Error: {0} {1}", ex.GetType(), ex.Message);
                MessageBox.Show(msg, "Caution", MessageBoxButton.OK);
            }

            return false;
        }

        private void ApplicationBarIconLoadButton_Click(object sender, EventArgs e)
        {
            if (photoChooserTask==null)
                photoChooserTask = new PhotoChooserTask();
            photoChooserTask.ShowCamera = true;
            photoChooserTask.Completed += new EventHandler<PhotoResult>(photoChooserTask_Completed);
            photoChooserTask.Show();
        }

        private void ApplicationBarIconSaveButton_Click(object sender, EventArgs e)
        {
            SaveImageToLibrary(true);
        }

        private void ApplicationBarIconColorButton_Click(object sender, EventArgs e)
        {
            changebutton = sender as ApplicationBarIconButton; 
            if(action == PhotoAction.Gray)
            {
                action = PhotoAction.Color;
                changebutton.Text = "Gray";
                changebutton.IconUri = new Uri("/Images/appbar.gray.png", UriKind.RelativeOrAbsolute);
                TileImage.Source = new BitmapImage(new Uri("/Images/StripeColorful.png", UriKind.RelativeOrAbsolute));
            }
            else
            {
                action = PhotoAction.Gray;
                changebutton.Text = "Color";
                changebutton.IconUri = new Uri("/Images/appbar.color.png", UriKind.RelativeOrAbsolute);
                TileImage.Source = new BitmapImage(new Uri("/Images/StripeBW.png", UriKind.RelativeOrAbsolute));
            }
        }

        private void ApplicationBarIconUndoButton_Click(object sender, EventArgs e)
        {
            if (imageMan == null)
                return;

            imageMan.UndoLast();
            image.Source = imageMan.finalImage;
        }

        private void ApplicationBarBrushMenuItem_Click(object sender, EventArgs e)
        {
            brushslider.Minimum = brush.MinRadius;
            brushslider.Maximum = brush.MaxRadius;
            brushslider.Value = brush.Size;
            brushpreview.Width = brush.Size * 2;
            brushpreview.Height = brush.Size * 2;
            brushPanel.Visibility = Visibility.Visible;
        }


        private void brushdefault_Click(object sender, RoutedEventArgs e)
        {
            //brushslider_ValueChanged(sender, new RoutedPropertyChangedEventArgs<double>(brushslider.Value, brush.DefaultRadius));
            brushslider.Value = brush.DefaultRadius;
        }

        private void brushok_Click(object sender, RoutedEventArgs e)
        {
            brush.Size = (int) brushslider.Value;
            brushPanel.Visibility = Visibility.Collapsed;
        }

        private void brushslider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            brushpreview.Width = e.NewValue * 2;
            brushpreview.Height = e.NewValue * 2;
        }

        private void PhoneApplicationPage_OrientationChanged(object sender, OrientationChangedEventArgs e)
        {
            if (imageMan == null)
                return;

            if ((e.Orientation & PageOrientation.Portrait) == (PageOrientation.Portrait))
            {
                image.Width = ContentPanel.ActualWidth;
                image.Height = (image.Width * imageMan.originalImage.PixelHeight) / imageMan.originalImage.PixelWidth;
            }
            else
            {
                image.Height = ContentPanel.ActualHeight;
                image.Width = (image.Height * imageMan.originalImage.PixelWidth) / imageMan.originalImage.PixelHeight;
            }

        }

        private void ApplicationBarReviewMenuItem_Click(object sender, EventArgs e)
        {
            MarketplaceReviewTask marketplaceReview = new MarketplaceReviewTask();
            marketplaceReview.Show();
        }

        private void ApplicationBarResetMenuItem_Click(object sender, EventArgs e)
        {
            if (imageMan != null)
            {
                imageMan.ResetPicture();
                image.Source = imageMan.finalImage;
            }
        }

        
    }
}