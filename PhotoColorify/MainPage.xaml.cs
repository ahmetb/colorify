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
using Microsoft.Xna.Framework.Media;

namespace Colorify
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        ImageManipulator imageMan;
        Brush brush = new Brush();
        private PhotoAction action = PhotoAction.Color;
        private string DEFAULT_WARNING_TEXT = "FIRST, PLEASE SELECT A PICTURE...";
        private ApplicationBarIconButton changebutton;
        private PhotoChooserTask photoChooserTask;
        private Point previous;
        private double zoomFactor = 3;
        private bool zoomed = false;
        private bool zooming = false;

        // these two fully define the zoom state:
        private double TotalImageScale = 1d;
        private Point ImagePosition = new Point(0, 0);

        private Point _oldFinger1;
        private Point _oldFinger2;
        private double _oldScaleFactor;

        public MainPage()
        {
            InitializeComponent();

            foreach (ApplicationBarIconButton button in this.ApplicationBar.Buttons)
            {
                if (button.Text.Equals("Color") || button.Text.Equals("Gray"))
                {
                    changebutton = button;
                    break;
                }
            }
        }


        private void ContentPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (imageMan == null)
                ApplicationBarIconLoadButton_Click(sender, e);

        }

        void gestureListener_DoubleTap(object sender, GestureEventArgs e)
        {
            if(imageMan!=null)
            {
                Point p = e.GetPosition(image);
                if (zoomed)
                {
                    zoomed = false;
                    ApplyZoomTransform(sender as Image, 1, p);
                }
                else
                {
                    zoomed = true;
                    ApplyZoomTransform(sender as Image, zoomFactor, p);
                }

                e.Handled = true;
            }
        }

        private void gestureListener_PinchStarted(object sender, PinchStartedGestureEventArgs e)
        {
            _oldFinger1 = e.GetPosition(image, 0);
            _oldFinger2 = e.GetPosition(image, 1);
            _oldScaleFactor = 1;
            zooming = true;
        }

        private void gestureListener_PinchDelta(object sender, PinchGestureEventArgs e)
        {
            if(!zooming)
                return;

            var scaleFactor = e.DistanceRatio / _oldScaleFactor;

            var currentFinger1 = e.GetPosition(image, 0);
            var currentFinger2 = e.GetPosition(image, 1);

            var translationDelta = GetTranslationDelta(
                currentFinger1,
                currentFinger2,
                _oldFinger1,
                _oldFinger2,
                ImagePosition,
                scaleFactor);

            _oldFinger1 = currentFinger1;
            _oldFinger2 = currentFinger2;
            _oldScaleFactor = e.DistanceRatio;

            UpdateImage(scaleFactor, translationDelta);
        }

        private void UpdateImage(double scaleFactor, Point delta)
        {
            TotalImageScale *= scaleFactor;
            if (TotalImageScale < 1)
                TotalImageScale = 1;
            else if (TotalImageScale > 5)
                TotalImageScale = 5;
                
            ImagePosition = new Point(ImagePosition.X + delta.X, ImagePosition.Y + delta.Y);

            var transform = image.RenderTransform as CompositeTransform;
            transform.ScaleX = TotalImageScale;
            transform.ScaleY = TotalImageScale;
            transform.TranslateX = ImagePosition.X;
            transform.TranslateY = ImagePosition.Y;
        }

        private Point GetTranslationDelta(
            Point currentFinger1, Point currentFinger2,
            Point oldFinger1, Point oldFinger2,
            Point currentPosition, double scaleFactor)
        {
            var newPos1 = new Point(
                currentFinger1.X + (currentPosition.X - oldFinger1.X) * scaleFactor,
                currentFinger1.Y + (currentPosition.Y - oldFinger1.Y) * scaleFactor);

            var newPos2 = new Point(
                currentFinger2.X + (currentPosition.X - oldFinger2.X) * scaleFactor,
                currentFinger2.Y + (currentPosition.Y - oldFinger2.Y) * scaleFactor);

            var newPos = new Point(
                (newPos1.X + newPos2.X) / 2,
                (newPos1.Y + newPos2.Y) / 2);

            return new Point(
                newPos.X - currentPosition.X,
                newPos.Y - currentPosition.Y);
        }


        private void gestureListener_PinchCompleted(object sender, PinchGestureEventArgs e)
        {
            zoomed = true;
            zooming = false;
        }

        void ApplyZoomTransform(Image element, double iZoomFactor, Point zoomCenter)
        {
            //get current transform
            var transform = image.RenderTransform as CompositeTransform;
            if (transform == null)
                transform = new CompositeTransform();
            
            if (zoomCenter != null)
            {
                transform.CenterX = zoomCenter.X;
                transform.CenterY = zoomCenter.Y;
            }
            transform.ScaleX = iZoomFactor;
            transform.ScaleY = iZoomFactor;

            element.RenderTransform = transform;

            int xClick = (int)(imageMan.finalImage.PixelWidth * (zoomCenter.X / image.Width));
            int yClick = (int)(imageMan.finalImage.PixelHeight * (zoomCenter.Y / image.Height));

            imageMan.UndoLastTwo(xClick, yClick);
            image.Source = imageMan.finalImage;
        }


       void photoChooserTask_Completed(object sender, PhotoResult e)
       {
            if(e.ChosenPhoto!=null)
            {
                StartLoadingImage(e.ChosenPhoto);
            }
            else if(imageMan==null)
            {
                warning.Text = DEFAULT_WARNING_TEXT;
                //((PhotoChooserTask)sender).Show();
            }
        }

       private void StartLoadingImage(Stream choosenPhoto)
        {
            iamgeProgressBar.IsIndeterminate = true;
            warning.Text = "LOADING GRAYSCALE PICTURE...";
            image.Visibility = Visibility.Collapsed;

            action = PhotoAction.Gray;
            ToogleBrush(changebutton, null);

            imageMan = new ImageManipulator(choosenPhoto, image, brush.Size);
            ThreadStart loadStart = new ThreadStart( () => {

                                                               DateTime Start = DateTime.Now;
                                                               imageMan.convertToBlackWhite();
                                                               int timeSpend = (int)(DateTime.Now - Start).TotalMilliseconds;
                    
                                                               if( timeSpend < 2000)
                                                               {
                                                                   Thread.Sleep(2000 - timeSpend);
                                                               }
                    
                                                               Dispatcher.BeginInvoke( () => {
                        
                                                                                                 image.RenderTransform = GetDefaultTransform();
                                                                                                 image.Width = ContentPanel.ActualWidth;
                                                                                                 image.Height = (image.Width*imageMan.originalImage.PixelHeight)/imageMan.originalImage.PixelWidth;
                                                                                                 image.Source = imageMan.finalImage;
                                                                                                 image.Visibility = Visibility.Visible;
                                                                                                 iamgeProgressBar.IsIndeterminate = false;
                                                                                                 warning.Text = "";

                                                               });

                                                               System.GC.Collect();
            } );

            Thread loadImage=new Thread(loadStart);
            loadImage.Start();
        }

        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //previous = e.GetPosition(image);
            image_MouseMove(sender, e);
        }

        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            if (imageMan != null && !zooming)
            {
                Point next = e.GetPosition(image);
                double actualHeight = image.ActualHeight;
                double actualWidth = image.ActualWidth;

                ThreadPool.QueueUserWorkItem((a) =>
                                                 {
                                                     int xClick = (int)(imageMan.finalImage.PixelWidth * (next.X / actualWidth));
                                                     int yClick = (int)(imageMan.finalImage.PixelHeight * (next.Y / actualHeight));
                                                     
                                                     imageMan.MofifyImage(action, xClick, yClick); 
                                                     
                                                     Dispatcher.BeginInvoke(() => {image.Source=imageMan.finalImage;});
                                                 });
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
                MediaLibrary library = new MediaLibrary();
                Picture picture = library.GetPictureFromToken(queryStrings["token"]);
                StartLoadingImage(picture.GetImage());
            }
        }

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            bool cancel = false;
            if(imageMan!=null && imageMan.modified)
            {
                var result = MessageBox.Show("Are you sure you want to exit? All unsaved images will be lost.", "Warning", MessageBoxButton.OKCancel);
                if(result == MessageBoxResult.Cancel)
                {
                    cancel = true;
                }
            }

            // cancel the navigation?
            e.Cancel = cancel;
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
                    MessageBox.Show("Image saved. You can share it with your friends and family. :)", "Tip", MessageBoxButton.OK);
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
            if (photoChooserTask == null)
            {
                photoChooserTask = new PhotoChooserTask();
                photoChooserTask.ShowCamera = true;
                photoChooserTask.Completed += new EventHandler<PhotoResult>(photoChooserTask_Completed);
            }

            try
            {
                photoChooserTask.Show();
            }
            catch (Exception)
            {
                
            }
        }

        private void ApplicationBarIconSaveButton_Click(object sender, EventArgs e)
        {
            SaveImageToLibrary(true);
        }

        private void ToogleBrush(object sender, EventArgs e)
        {
            //changebutton = sender as ApplicationBarIconButton; 
            if(action == PhotoAction.Gray)
            {
                action = PhotoAction.Color;
                changebutton.Text = "Gray";
                ApplicationTitle.Text = "NOW COLORING";
                ApplicationTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0)); 
                changebutton.IconUri = new Uri("/Images/appbar.gray.png", UriKind.RelativeOrAbsolute);
                TileImage.Source = new BitmapImage(new Uri("/Images/StripeColorful.png", UriKind.RelativeOrAbsolute));
            }
            else
            {
                action = PhotoAction.Gray;
                changebutton.Text = "Color";
                ApplicationTitle.Text = "NOW ERASING";
                ApplicationTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
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
            brushslider.Minimum = Brush.MinRadius;
            brushslider.Maximum = Brush.MaxRadius;
            brushslider.Value = brush.Size;
            brushpreview.Width = brush.Size * 2;
            brushpreview.Height = brush.Size * 2;
            brushPanel.Visibility = Visibility.Visible;
        }


        private void brushdefault_Click(object sender, RoutedEventArgs e)
        {
            //brushslider_ValueChanged(sender, new RoutedPropertyChangedEventArgs<double>(brushslider.Value, brush.DefaultRadius));
            brushslider.Value = Brush.DefaultRadius;
        }

        private void brushok_Click(object sender, RoutedEventArgs e)
        {
            brush.Size = (int) brushslider.Value;
            imageMan.Radius = (int)((imageMan.finalImage.PixelWidth * brush.Size) / image.Width);
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
                image.RenderTransform = GetDefaultTransform();
                image.Source = imageMan.finalImage;
                zoomed = false;
                zooming = false;
            }
        }

        private CompositeTransform GetDefaultTransform()
        {
            CompositeTransform transform = new CompositeTransform();
            transform.ScaleX = 1;
            transform.ScaleY = 1;
            transform.CenterX = image.Width / 2;
            transform.CenterY = image.Height / 2;

            return transform;
        }

        private void ApplicationBarFeedbackMenuItem_Click(object sender, EventArgs e)
        {
            App.SendEmail(App.COLORIFY_EMAIL, "Feedback", "");
        }

        private void TitlePanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if(changebutton != null)
            {
                ToogleBrush(changebutton, null);
            }
        }
    }
}