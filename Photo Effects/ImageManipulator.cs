using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Xna.Framework.Media;

namespace Photo_Colorify
{
    public enum PhotoAction
    {
        Gray,
        Color
    }

    public class Effect
    {
        internal PhotoAction action;
        internal int xClick;
        internal int yClick;
        internal int cRaidus;

        public Effect(PhotoAction action, int xClick, int yClick, int cRaidus)
        {
            this.action = action;
            this.xClick = xClick;
            this.yClick = yClick;
            this.cRaidus = cRaidus;
        }
    }

    public class Brush
    {
        
        public int MinRadius = 10;
        public int MaxRadius = 70;
        public int DefaultRadius = 20;
        private int size = 15;

        public int Size
        {
            get { return size; }
            set 
            {
                if (value > MaxRadius)
                    size = MaxRadius;
                else if (value < MinRadius)
                    size = MaxRadius;
                else
                    size = value;
            }
        }


    }

    public class ImageManipulator
    {
        static readonly float[][] matrix = new float[][]{ 
                new float[] {1, 0, 0, 0},
                new float[] {0, .3f, .3f, .3f}, 
                new float[] {0, .59f, .59f, .59f},  
                new float[] {0, .11f, .11f, .11f}               
            };

        public WriteableBitmap originalImage;
        private int[] originalImagePixels;
        private int originalImagePixelsLength;
        //public WriteableBitmap grayImage;
        private int[] grayImagePixels;
        public WriteableBitmap finalImage;
        private int[] finalImagePixels;
        public string lastSavedFileName;

        private Stack<Effect> doneEffects = new Stack<Effect>(20);
        public bool modified = true;

        public ImageManipulator(string token, Image helper)
        {
            // Retrieve the picture from the media library using the token passed to the application.
            MediaLibrary library = new MediaLibrary();
            Picture picture = library.GetPictureFromToken(token);

            InitFromStream(picture.GetImage(), helper);
        }

        public ImageManipulator(Stream resource, Image helper)
        {
            InitFromStream(resource, helper);
        }

        private void InitFromStream(Stream resource, Image helper)
        {
            var originalBitmapImage = new BitmapImage();
            //originalBitmapImage.CreateOptions = BitmapCreateOptions.None;
            originalBitmapImage.SetSource(resource);
            var tmp = helper.Source;
            helper.Source = originalBitmapImage;

            originalImage = new WriteableBitmap(helper.Source as BitmapImage);
            helper.Source = tmp;

            //grayImage = new WriteableBitmap(originalImage.PixelWidth, originalImage.PixelHeight);
            finalImage = new WriteableBitmap(originalImage.PixelWidth, originalImage.PixelHeight);
        }

        void bitmapImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            originalImage = new WriteableBitmap((BitmapImage)sender);
            convertToBlackWhite();
        }

        public void convertToBlackWhite()
        {
            originalImagePixels = originalImage.Pixels;
            originalImagePixelsLength = originalImagePixels.Length;
            //grayImagePixels = grayImage.Pixels;
            grayImagePixels = new int[originalImagePixelsLength];
            finalImagePixels = finalImage.Pixels;


            for (int i = 0; i < originalImagePixelsLength; i++)
            {
                byte[] pixel = BitConverter.GetBytes(originalImagePixels[i]);
                byte[] newpixel = 
                { 
                    (byte)(pixel[3] * matrix[0][3] + pixel[2] * matrix[1][3] + pixel[1] * matrix[2][3] + pixel[0] * matrix[3][3]), //newb
                    (byte)(pixel[3] * matrix[0][2] + pixel[2] * matrix[1][2] + pixel[1] * matrix[2][2] + pixel[0] * matrix[3][2]), //newg
                    (byte)(pixel[3] * matrix[0][1] + pixel[2] * matrix[1][1] + pixel[1] * matrix[2][1] + pixel[0] * matrix[3][1]), //newr
                    255 //newa
                };

                /*
                byte newa = 255; // multiply(pixel, 0);
                byte newr = (byte)(pixel[3] * matrix[0][1] + pixel[2] * matrix[1][1] + pixel[1] * matrix[2][1] + pixel[0] * matrix[3][1]); // multiply(pixel, 1);
                byte newg = (byte)(pixel[3] * matrix[0][2] + pixel[2] * matrix[1][2] + pixel[1] * matrix[2][2] + pixel[0] * matrix[3][2]); // multiply(pixel, 2);
                byte newb = (byte)(pixel[3] * matrix[0][3] + pixel[2] * matrix[1][3] + pixel[1] * matrix[2][3] + pixel[0] * matrix[3][3]); // multiply(pixel, 3);
                byte[] newpixel = { newb, newg, newr, newa };
                */

                int newpixelvalue =  BitConverter.ToInt32(newpixel, 0);
                finalImagePixels[i] = newpixelvalue;
                grayImagePixels[i] = newpixelvalue;
            }

          
        }

        private byte multiply(byte[] pixel, int col)
        {
            return (byte)(pixel[3] * matrix[0][col] + pixel[2] * matrix[1][col] + pixel[1] * matrix[2][col] + pixel[0] * matrix[3][col]);
        }


        public void MofifyImage(PhotoAction action,int xClick, int yClick, int cRadius)
        {
            Effect effect = new Effect(action, xClick, yClick, cRadius);
            switch (action)
            {
                case PhotoAction.Gray:
                    MofifyImageImpl(grayImagePixels, xClick, yClick, cRadius);
                    doneEffects.Push(effect);
                    break;

                case PhotoAction.Color:
                    MofifyImageImpl(originalImagePixels, xClick, yClick, cRadius);
                    doneEffects.Push(effect);
                    break;                 
            }
            
        }

        public void MofifyImage(Effect effect)
        {
            switch (effect.action)
            {
                case PhotoAction.Gray:
                    MofifyImageImpl(originalImagePixels, effect.xClick, effect.yClick, effect.cRaidus);
                    break;

                case PhotoAction.Color:
                    MofifyImageImpl(grayImagePixels, effect.xClick, effect.yClick, effect.cRaidus);
                    break;
            }

        }

        private void MofifyImageImpl(int [] srcPixels, int xClick, int yClick, int cRadius)
        {
            int pixelWidth = finalImage.PixelWidth;
            int pixelHeight = finalImage.PixelHeight;
            
            int ci, xs, xe;
            int center = yClick * pixelWidth + xClick;
            double cRadiussqr = cRadius * cRadius;
            
            for (int yi = 0; yi < cRadius; yi++)
            {
                int sx = (int)Math.Sqrt(cRadiussqr - yi * yi);
                if (yClick + yi < pixelHeight)
                {
                    xs = -sx;
                    xe = sx; 
                    if (xClick + sx > pixelWidth)
                        xe = pixelWidth - xClick;
                    else if (xClick - sx < 0)
                        xs = 0;

                    ci = center + xs + yi * pixelWidth;

                    modified = true;
                    Array.Copy(srcPixels, ci, finalImagePixels, ci, xe - xs);
                }

                if (yClick - yi >= 0)
                {
                    xs = -sx;
                    xe = sx;
                    if (xClick + sx > pixelWidth)
                        xe = pixelWidth - xClick;
                    else if (xClick - sx < 0)
                        xs = 0;

                    ci = center + xs - yi * pixelWidth;

                    modified = true;
                    Array.Copy(srcPixels, ci, finalImagePixels, ci, xe - xs);
                }
            }
        }

        public void UndoLast()
        {
            int index = doneEffects.Count - 1;
            if(index >= 0)
            {
                Effect effect = doneEffects.Pop();
                MofifyImage(effect);
            }
        }


        public void SaveToMediaLibrary()
        {
            MediaLibrary mediaLibrary = new MediaLibrary();
            string saveFilename = Guid.NewGuid().ToString() +".jpg";

            MemoryStream ms = new MemoryStream(originalImagePixelsLength); //Will be compressed.
            finalImage.SaveJpeg(ms, finalImage.PixelWidth, finalImage.PixelHeight, 0, 100);
            ms.Position = 0;
            using (ms)
            {
                Picture picture=mediaLibrary.SavePicture(saveFilename, ms);
                lastSavedFileName = picture.Name;
                modified = false;
                ms.Close();
            }
            
        }

        public void ResetPicture()
        {
            Array.Copy(grayImagePixels, finalImagePixels, originalImagePixelsLength);
            doneEffects.Clear();
            modified = true;
        }
    }

}
