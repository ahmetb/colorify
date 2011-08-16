// Type: System.Windows.Media.Imaging.BitmapImage
// Assembly: System.Windows, Version=2.0.5.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e
// Assembly location: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\Silverlight\v4.0\Profile\WindowsPhone\System.Windows.dll

using System;
using System.Windows;

namespace System.Windows.Media.Imaging
{
    public sealed class BitmapImage : BitmapSource
    {
        public static readonly DependencyProperty CreateOptionsProperty;
        public static readonly DependencyProperty UriSourceProperty;
        public BitmapImage(Uri uriSource);
        public BitmapImage();
        public BitmapCreateOptions CreateOptions { get; set; }
        public Uri UriSource { get; set; }
        public event EventHandler<DownloadProgressEventArgs> DownloadProgress;
        public event EventHandler<ExceptionRoutedEventArgs> ImageFailed;
        public event EventHandler<RoutedEventArgs> ImageOpened;
    }
}
