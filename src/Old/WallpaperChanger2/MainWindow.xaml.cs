﻿using Microsoft.Win32;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Verloka.HelperLib.Update;
using WallpaperChanger2.Model;
using WallpaperChanger2.Windows;

namespace WallpaperChanger2
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        Timer timer;
        System.Windows.Forms.NotifyIcon notifyIcon;

        UpdateClient updateClient;
        UpdateItem updateContent;
        const string UpdateUrl = "https://ogycode.github.io/WallpaperChanger/update.json";

        public MainWindow()
        {
            InitializeComponent();
        }

        public void SetupImage(Uri uri, Model.Style style)
        {
            Stream s = new System.Net.WebClient().OpenRead(uri.ToString());

            System.Drawing.Image img = System.Drawing.Image.FromStream(s);
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wallpaper.bmp");
            img.Save(tempPath, System.Drawing.Imaging.ImageFormat.Bmp);

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);

            switch (style)
            {
                case Model.Style.Tiled:
                    key.SetValue(@"WallpaperStyle", 1.ToString());
                    key.SetValue(@"TileWallpaper", 1.ToString());
                    break;
                case Model.Style.Centered:
                    key.SetValue(@"WallpaperStyle", 1.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                    break;
                default:
                case Model.Style.Stretched:
                    key.SetValue(@"WallpaperStyle", 2.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                    break;
            }

            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }
        public async void Build(bool Update = false)
        {
            if (!GetConnection())
            {
                if (App.Settings.GetValue("ShowAlertInternetMsg", false))
                    Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => new MessageWindow().ShowDialog("Warning!!!", "To update wallpaper on your desktop, you need internet")));
                return;
            }

            string uid = App.Settings.GetValue("ImageUID", "");

            string url = await Bing.ImageUrl();

            if (CheckDate() || Update)
                if (uid != url)
                    Set(url);
        }
        public bool CheckDate()
        {
            DateTime dt = new DateTime(App.Settings.GetValue("UpdateWallpaperYear", 2000),
                                       App.Settings.GetValue("UpdateWallpaperMounth", 1),
                                       App.Settings.GetValue("UpdateWallpaperDay", 1),
                                       App.Settings.GetValue("UpdateWallpaperHour", 1), 0, 0);


            return (DateTime.Now > dt);
        }
        public bool GetConnection()
        {
            try
            {
                using (var client = new WebClient())
                using (var stream = client.OpenRead("https://www.google.com"))
                    return true;
            }
            catch { return false; }
        }
        double GetMilisec(int minunte)
        {
            return 60000 * minunte;
        }
        void Set(string url)
        {
            SetupImage(new Uri(url, UriKind.RelativeOrAbsolute), Model.Style.Stretched);
            var dt = DateTime.Now.Add(getTimeSpan(App.Settings.GetValue<int>("Timetable")));

            App.Settings["UpdateWallpaperYear"] = dt.Year;
            App.Settings["UpdateWallpaperMounth"] = dt.Month;
            App.Settings["UpdateWallpaperDay"] = dt.Day;
            App.Settings["UpdateWallpaperHour"] = dt.Hour;

            App.Settings["ImageUID"] = url;
        }
        TimeSpan getTimeSpan(int timetable)
        {
            TimeSpan ts;

            switch (timetable)
            {
                case 0:
                default:
                    ts = new TimeSpan(1, 0, 0, 0);
                    break;
                case 1:
                    ts = new TimeSpan(2, 0, 0, 0);
                    break;
                case 2:
                    ts = new TimeSpan(3, 0, 0, 0);
                    break;
                case 3:
                    ts = new TimeSpan(7, 0, 0, 0);
                    break;
                case 4:
                    ts = new TimeSpan(14, 0, 0, 0);
                    break;
                case 5:
                    ts = new TimeSpan(21, 0, 0, 0);
                    break;
                case 6:
                    ts = new TimeSpan(30, 0, 0, 0);
                    break;
            }

            return ts;
        }

        #region Window Events
        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); }
            catch { }
        }
        private void btnCloseClick()
        {
            if (App.Settings.GetValue<bool>("AppExit"))
            {
                Application.Current.Shutdown(0);
            }
            else
            {
                Hide();
            }
        }
        private void btnMinimazeClick()
        {
            WindowState = WindowState.Minimized;
        }
        private void btnHelpClick()
        {
            new Windows.InfoWindow().ShowDialog();
        }
        #endregion

        private void mywindowLoaded(object sender, RoutedEventArgs e)
        {
            //timer
            timer = new Timer(GetMilisec(60));
            timer.Elapsed += TimerElapsed;
            timer.Enabled = true;

            //notify
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Text = "Wallpaper Changer 2";
            notifyIcon.Icon = Properties.Resources.AppIcon;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += NotifyIconDoubleClick;
            notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add("Update wallpaper").Click += UpdateWallpaerClick;
            notifyIcon.ContextMenuStrip.Items.Add("Exit").Click += ExitClick;

            //app exit
            Application.Current.Exit += CurrentExit;

            //startup
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if ((string)key.GetValue("Wallpaper Changer 2") == null)
                cbStartup.IsChecked = false;
            else
                cbStartup.IsChecked = true;
            cbStartup.Click += CbStartupClick;

            //theme
            cbTheme.SelectedIndex = App.Settings.GetValue("Theme", 0);
            cbTheme.SelectionChanged += CbThemeSelectionChanged;

            //app exit when collsing
            cbExit.IsChecked = App.Settings.GetValue("AppExit", false);
            cbExit.Click += CbExitClick;

            //show msg
            cbAlertInternet.IsChecked = App.Settings.GetValue("ShowAlertInternetMsg", false);
            cbAlertInternet.Click += CbAlertInternetClick;

            //cbTimetable
            cbTimetable.SelectedIndex = App.Settings.GetValue("Timetable", 0);
            cbTimetable.SelectionChanged += CbTimetableSelectionChanged;

            //update
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;

            updateClient = new UpdateClient(UpdateUrl);
            updateClient.WebException += UpdateClientWebException;
            updateClient.NewVersion += UpdateClientNewVersion;
            updateClient.Check(new Verloka.HelperLib.Update.Version(version.Major, version.Minor, version.MajorRevision, version.MinorRevision));

            //setup img
            Build();
        }
        private void CbTimetableSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            App.Settings["Timetable"] = cbTimetable.SelectedIndex;
        }
        private void UpdateClientWebException(WebException obj)
        {
            Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => new MessageWindow().ShowDialog("Warning!!!", obj.Message)));
        }
        private void UpdateClientNewVersion(UpdateItem item)
        {
            updateBanner.Visibility = Visibility.Visible;
            tbNewVersion.Text = $"New version is available - {item.VersionNumber.Major}.{item.VersionNumber.Minor}.{item.VersionNumber.Revision}.{item.VersionNumber.Build}";
            updateContent = item;
        }
        private void CbAlertInternetClick(object sender, RoutedEventArgs e)
        {
            App.Settings["ShowAlertInternetMsg"] = cbAlertInternet.IsChecked.Value;
        }
        private void CbExitClick(object sender, RoutedEventArgs e)
        {
            App.Settings["AppExit"] = cbExit.IsChecked.Value;
        }
        private void CbThemeSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            App.Settings["Theme"] = cbTheme.SelectedIndex;
            App.UpdateTheme(cbTheme.SelectedIndex);
            UpdateDefaultStyle();
        }
        private void CbStartupClick(object sender, RoutedEventArgs e)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (!cbStartup.IsChecked.Value)
                key.DeleteValue("Wallpaper Changer 2", false);
            else
                key.SetValue("Wallpaper Changer 2", $"\"{Assembly.GetExecutingAssembly().Location}\" -silent");
        }
        private void CurrentExit(object sender, ExitEventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            notifyIcon = null;
            timer.Dispose();
            timer = null;
        }
        private void ExitClick(object sender, EventArgs e)
        {
            Application.Current.Shutdown(0);
        }
        private void UpdateWallpaerClick(object sender, EventArgs e)
        {
            Build(true);
        }
        private void NotifyIconDoubleClick(object sender, EventArgs e)
        {
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        }
        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            Build();
        }
        private void btnUpdateClick()
        {
            if (updateContent == null)
                return;

            notifyIcon.Visible = false;
            Hide();

            new UpdateWindow(updateContent).Show();
        }
    }
}
