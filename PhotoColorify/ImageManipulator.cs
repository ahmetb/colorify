using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Xna.Framework.Media;

namespace Colorify
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
        internal bool marker;

        public Effect(PhotoAction action, int xClick, int yClick, int cRaidus, bool marker)
        {
            this.action = action;
            this.xClick = xClick;
            this.yClick = yClick;
            this.cRaidus = cRaidus;
            this.marker = marker;
        }
    }

    public class Brush
    {
        public const int MinRadius = 10;
        public const int MaxRadius = 70;
        public const int DefaultRadius = 15;

        private int size = 15;

        private readonly static string BRUSH_KEY = "brush_key";
        
        public Brush()
        {
            size = SettingsProvider.Get(BRUSH_KEY, DefaultRadius);
        }

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

                SettingsProvider.Set(BRUSH_KEY, size);
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
        private int radius = 0;
        private int[] xcordinates;
        //private int InvalidClickX;
        //private int InvalidClickY;
        private int InvalidCount = 0;
        private int SaveId = 0;
        private int marker = 0;

        private readonly static string FILENAME_KEY = "filename_key";

        public ImageManipulator(string token, Image helper, int bsize)
        {
            // Retrieve the picture from the media library using the token passed to the application.
            MediaLibrary library = new MediaLibrary();
            Picture picture = library.GetPictureFromToken(token);

            InitFromStream(picture.GetImage(), helper);
            this.Radius = (int)((finalImage.PixelWidth * bsize) / helper.Width);
        }

        public ImageManipulator(Stream resource, Image helper, int bsize)
        {
            InitFromStream(resource, helper);
            this.Radius = (int)((finalImage.PixelWidth * bsize) / helper.Width);
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

        public int Radius
        {
            get { return radius; }
            set
            {
                radius = value;
                xcordinates = new int[radius];
                double cRadiusSqr = radius * radius;
                for (int yi = 0; yi < radius; yi++)
                {
                    xcordinates[yi] = (int)Math.Sqrt(cRadiusSqr - yi * yi);
                }
            }
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


        public void setMarker()
        {
            Interlocked.CompareExchange(ref marker, 1, 0);
        }

        public void MofifyImage(PhotoAction action,int xClick, int yClick)
        {
            if (InvalidCount > 0)
            {
                InvalidCount--;
                return;
            }

            bool mark = Interlocked.CompareExchange(ref marker, 0, 1) == 1;
            Effect effect = new Effect(action, xClick, yClick, radius, mark);
            switch (action)
            {
                case PhotoAction.Gray:
                    MofifyImageImpl(grayImagePixels, xClick, yClick, radius);
                    doneEffects.Push(effect);
                    break;

                case PhotoAction.Color:
                    MofifyImageImpl(originalImagePixels, xClick, yClick, radius);
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
            modified = true;
            int pixelWidth = finalImage.PixelWidth;
            int pixelHeight = finalImage.PixelHeight;

            int centeri;
            int center = yClick * pixelWidth + xClick;
            

            for (int yi = 0; yi < cRadius; yi++)
            {
                int sx = xcordinates[yi];

                if (yClick + yi < pixelHeight)
                {
                    centeri = center - sx + yi * pixelWidth;
                    try
                    {
                        Array.Copy(srcPixels, centeri, finalImagePixels, centeri, sx * 2);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                if (yClick - yi >= 0)
                {
                    centeri = center - sx - yi * pixelWidth;
                    try
                    {
                        Array.Copy(srcPixels, centeri, finalImagePixels, centeri, sx * 2);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
        }

        public void UndoLastPaints()
        {
            while (doneEffects.Count > 0)
            {
                Effect effect = doneEffects.Pop();
                MofifyImage(effect);

                if (effect.marker)
                    break;
            }
        }

        private int CalculateError(int x1, int x2, int y1, int y2)
        {
            double result = (100.0 * Math.Abs(x1 - x2)) / finalImage.PixelWidth;
            result +=  (100.0 * Math.Abs(y1 - y2)) / finalImage.PixelHeight;

            return (int)result;
        }

        public void UndoLastTwo(int xClick, int yClick)
        {
            int count = 0;
            int error = 20;
            const int upper = 2;

            for (int i = 0; i < upper; i++)
            {
                if (doneEffects.Count > 0)
                {
                    Effect effect = doneEffects.Peek();
                    if (CalculateError(effect.xClick, xClick, effect.yClick, yClick) < error)
                    {
                        UndoLastPaints();
                    }
                }
            }

            if (count < upper)
            {
                this.InvalidCount = upper - count;
            }
        }
        
        public void SaveToMediaLibrary()
        {
            MediaLibrary mediaLibrary = new MediaLibrary();
            string saveFilename = GetSaveFileName();

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

        private string GetSaveFileName()
        {
            return Guid.NewGuid().ToString() + ".jpg";
        }

        private string GetSaveFileName2()
        {
            SaveId = SettingsProvider.Get(FILENAME_KEY, SaveId);
            SaveId++;
            SettingsProvider.Set(FILENAME_KEY, SaveId);

            string result = string.Format("PC_{0}.jpg", SaveId.ToString("D4"));
            return result;
        }

        public void ResetPicture()
        {
            Array.Copy(grayImagePixels, finalImagePixels, originalImagePixelsLength);
            doneEffects.Clear();
            modified = true;
        }

    }

}
