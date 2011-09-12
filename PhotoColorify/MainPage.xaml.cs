using System;
using System.Collections.Generic;
using System.IO;
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
        private int zooming = 0;

        // these two fully define the zoom state:
        private double TotalImageScale = 1d;
        private Point ImagePosition = new Point(0, 0);
        private const double MAX_IMAGE_ZOOM = 5;
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


        private void gestureListener_PinchStarted(object sender, PinchStartedGestureEventArgs e)
        {
            if (imageMan == null)
                return;

            Interlocked.CompareExchange(ref zooming, 1, 0);
            _oldFinger1 = e.GetPosition(image, 0);
            _oldFinger2 = e.GetPosition(image, 1);
            _oldScaleFactor = 1;
            image.Source = imageMan.finalImage;
        }

        private void gestureListener_PinchDelta(object sender, PinchGestureEventArgs e)
        {
            if (imageMan == null)
                return;

            Interlocked.CompareExchange(ref zooming, 1, 0);
            var scaleFactor = e.DistanceRatio / _oldScaleFactor;
            if (!IsScaleValid(scaleFactor))
                return;

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

            UpdateImageScale(scaleFactor);
            UpdateImagePosition(translationDelta);
        }

        private void gestureListener_PinchCompleted(object sender, PinchGestureEventArgs e)
        {
            if (imageMan == null)
                return;

            zoomed = true;
            Interlocked.Exchange(ref zooming, 0);
        }

        private void gestureListener_DoubleTap(object sender, GestureEventArgs e)
        {
            if (imageMan == null)
                return;
                        
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
        }

        #region Utils

        /// <summary>
        /// Computes the translation needed to keep the image centered between your fingers.
        /// </summary>
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

        /// <summary>
        /// Updates the scaling factor by multiplying the delta.
        /// </summary>
        private void UpdateImageScale(double scaleFactor)
        {
            TotalImageScale *= scaleFactor;
            ApplyScale();
        }

        /// <summary>
        /// Applies the computed scale to the image control.
        /// </summary>
        private void ApplyScale()
        {
            ((CompositeTransform)image.RenderTransform).ScaleX = TotalImageScale;
            ((CompositeTransform)image.RenderTransform).ScaleY = TotalImageScale;
        }

        /// <summary>
        /// Updates the image position by applying the delta.
        /// Checks that the image does not leave empty space around its edges.
        /// </summary>
        private void UpdateImagePosition(Point delta)
        {
            var newPosition = new Point(ImagePosition.X + delta.X, ImagePosition.Y + delta.Y);

            if (newPosition.X > 0) newPosition.X = 0;
            if (newPosition.Y > 0) newPosition.Y = 0;

            if ((image.ActualWidth * TotalImageScale) + newPosition.X < image.ActualWidth)
                newPosition.X = image.ActualWidth - (image.ActualWidth * TotalImageScale);

            if ((image.ActualHeight * TotalImageScale) + newPosition.Y < image.ActualHeight)
                newPosition.Y = image.ActualHeight - (image.ActualHeight * TotalImageScale);

            ImagePosition = newPosition;

            ApplyPosition();
        }

        /// <summary>
        /// Applies the computed position to the image control.
        /// </summary>
        private void ApplyPosition()
        {
            ((CompositeTransform)image.RenderTransform).TranslateX = ImagePosition.X;
            ((CompositeTransform)image.RenderTransform).TranslateY = ImagePosition.Y;
        }

        /// <summary>
        /// Resets the zoom to its original scale and position
        /// </summary>
        private void ResetImagePosition()
        {
            TotalImageScale = 1;
            ImagePosition = new Point(0, 0);
            ApplyScale();
            ApplyPosition();
        }

        /// <summary>
        /// Checks that dragging by the given amount won't result in empty space around the image
        /// </summary>
        private bool IsDragValid(double scaleDelta, Point translateDelta)
        {
            if (ImagePosition.X + translateDelta.X > 0 || ImagePosition.Y + translateDelta.Y > 0)
                return false;

            if ((image.ActualWidth * TotalImageScale * scaleDelta) + (ImagePosition.X + translateDelta.X) < image.ActualWidth)
                return false;

            if ((image.ActualHeight * TotalImageScale * scaleDelta) + (ImagePosition.Y + translateDelta.Y) < image.ActualHeight)
                return false;

            return true;
        }

        /// <summary>
        /// Tells if the scaling is inside the desired range
        /// </summary>
        private bool IsScaleValid(double scaleDelta)
        {
            return (TotalImageScale * scaleDelta >= 1) && (TotalImageScale * scaleDelta <= MAX_IMAGE_ZOOM);
        }

        #endregion


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
            transform.TranslateX = 0;
            transform.TranslateY = 0;

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
                warning.InvalidateArrange();
                //((PhotoChooserTask)sender).Show();
            }

            ApplicationLicense.CheckTrialMode(adControl);
       }

       private void StartLoadingImage(Stream choosenPhoto)
       {
            image.Visibility = Visibility.Collapsed;
            warning.Text = "LOADING GRAYSCALE PICTURE...";
            ColoringTitle.Text = "";
            TileImage.Source = null;
            iamgeProgressBar.IsIndeterminate = true;
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

                                warning.Text = "";
                                action = PhotoAction.Gray;
                                ToogleBrush(changebutton, null);

                                image.RenderTransform = GetDefaultTransform();
                                image.Width = ContentPanel.ActualWidth;
                                image.Height = (image.Width*imageMan.originalImage.PixelHeight)/imageMan.originalImage.PixelWidth;
                                image.Source = imageMan.finalImage;
                                image.Visibility = Visibility.Visible;
                                iamgeProgressBar.IsIndeterminate = false;

                        });

                        System.GC.Collect();
            } );

            Thread loadImage=new Thread(loadStart);
            loadImage.Start();
        }

        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (imageMan!=null && Interlocked.CompareExchange(ref zooming, zooming, zooming)!=1)
            {
                imageMan.setMarker();
                previous = e.GetPosition(image);
                image_MouseMove(sender, e);
            }
        }

        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            if (imageMan!=null)
            {
                Point next = e.GetPosition(image);
                if (Interlocked.CompareExchange(ref zooming, zooming, zooming) == 1)
                {
                    previous = next;
                    return;
                }
                double actualHeight = image.ActualHeight;
                double actualWidth = image.ActualWidth;

                double xdiff= next.X - previous.X;
                double ydiff= next.Y - previous.Y;
                double deltarad = Math.Sqrt(xdiff*xdiff + ydiff*ydiff);
                if(deltarad > brush.Size)
                {
                    int maxdevide = (int) (deltarad/brush.Size)+1;
                    Point pp = previous;
                    for(int i=0; i<maxdevide; i++)
                    {
                        PaintPoint(actualWidth, actualHeight, pp);
                        pp.X += xdiff / maxdevide;
                        pp.Y += ydiff / maxdevide;
                    }
                }
                else
                {
                    PaintPoint(actualWidth, actualHeight, next);
                }
                previous = next;
            }
        }

        private void PaintPoint(double actualWidth, double actualHeight, Point next)
        {
            ThreadPool.QueueUserWorkItem((a) =>
            {
                int xClick = (int) (imageMan.finalImage.PixelWidth*(next.X/actualWidth));
                int yClick = (int) (imageMan.finalImage.PixelHeight*(next.Y/actualHeight));

                imageMan.MofifyImage(action, xClick, yClick);

                Dispatcher.BeginInvoke(() => { image.Source = imageMan.finalImage; });
            });
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

            if(brushPanel.Visibility == Visibility.Visible)
            {
                brushPanel.Visibility = Visibility.Collapsed;
                cancel = true;
            }
            else if(imageMan!=null && imageMan.modified)
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
                ColoringTitle.Text = "NOW COLORING";
                ColoringTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0)); 
                changebutton.IconUri = new Uri("/Images/appbar.gray.png", UriKind.RelativeOrAbsolute);
                TileImage.Source = new BitmapImage(new Uri("/Images/StripeColorful.png", UriKind.RelativeOrAbsolute));
            }
            else
            {
                action = PhotoAction.Gray;
                changebutton.Text = "Color";
                ColoringTitle.Text = "NOW ERASING";
                ColoringTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                changebutton.IconUri = new Uri("/Images/appbar.color.png", UriKind.RelativeOrAbsolute);
                TileImage.Source = new BitmapImage(new Uri("/Images/StripeBW.png", UriKind.RelativeOrAbsolute));
            }
        }

        private void ApplicationBarIconUndoButton_Click(object sender, EventArgs e)
        {
            if (imageMan == null)
                return;

            imageMan.UndoLastPaints();
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
            //brushslider.Focus();
        }


        private void brushdefault_Click(object sender, RoutedEventArgs e)
        {
            //brushslider_ValueChanged(sender, new RoutedPropertyChangedEventArgs<double>(brushslider.Value, brush.DefaultRadius));
            brushslider.Value = Brush.DefaultRadius;
        }

        private void brushok_Click(object sender, RoutedEventArgs e)
        {
            brush.Size = (int) brushslider.Value;
            if(imageMan!=null)
            {
                imageMan.Radius = (int)((imageMan.finalImage.PixelWidth * brush.Size) / image.Width);
            }
            brushPanel.Visibility = Visibility.Collapsed;
        }

        private void brushslider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            brushpreview.Width = e.NewValue * 2;
            brushpreview.Height = e.NewValue * 2;
        }

        private void PhoneApplicationPage_OrientationChanged(object sender, OrientationChangedEventArgs e)
        {
            bool portrait = ((e.Orientation & PageOrientation.Portrait)==(PageOrientation.Portrait));

            if (imageMan == null)
                return;
            
            if (portrait)
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
                ResetImagePosition();
                image.Source = imageMan.finalImage;
                zoomed = false;
                zooming = 0;
            }
        }

        private CompositeTransform GetDefaultTransform()
        {
            CompositeTransform transform = new CompositeTransform();
            transform.TranslateX = 0;
            transform.TranslateY = 0;
            transform.ScaleX = 1;
            transform.ScaleY = 1;
            transform.CenterX = image.ActualWidth / 2;
            transform.CenterY = image.ActualHeight / 2;

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
        
        private void adControl_ErrorOccurred(object sender, Microsoft.Advertising.AdErrorEventArgs e)
        {
            adControl.Refresh();
        }
    }
}