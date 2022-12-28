using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Shell;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RapidGrab_file_downloader
{
    public partial class Form1 : Form
    {
        #region
        private const string DOWNLOAD_STARTED = "Download started";
        private const string DOWNLOAD_PAUSED = "Download paused";
        private const string DOWNLOAD_RESUMED = "Download resumed";
        private const string DOWNLOAD_CANCELLED = "Download cancelled";
        private const string DOWNLOAD_COMPLETED = "Download completed";
        private const string DOWNLOAD_ERROR = "Error occurred during download";

        private const string ICON_DEFAULT = "default";
        private const string ICON_ERROR = "error";
        private const string ICON_SUCCESS = "success";
        private const string ICON_WARNING = "warning";

        private const ToolTipIcon DEFAULT_iCON = ToolTipIcon.None;
        private const ToolTipIcon ERROR_ICON = ToolTipIcon.Error;
        private const ToolTipIcon SUCCESS_ICON = ToolTipIcon.Info;
        private const ToolTipIcon WARNING_ICON = ToolTipIcon.Warning;

        private DateTime startTime = DateTime.Now;
        private byte[] buffer;
        private int bytesRead;
        private double timeRemainingSeconds;

        private Thread downloadThread;

        private bool isPaused;
        private bool isCancelled;
        private bool isdownloadInProgress;

        private HttpWebRequest webRequest;
        private HttpWebResponse webResponse;

        private Stream stream;
        private FileStream fileStream;

        private string downloadUrl;
        private string fileName;
        private string savePath;

        private long fileSize;
        private long bytesDownloaded;
        private double downloadSpeed;
        private int downloadProgress;

        private static Uri uriString;

        private TimeSpan timeRemaining;

        private readonly SaveFileDialog saveFileDialog = new SaveFileDialog();
#endregion

        public Form1()
        {
            InitializeComponent();

            jumper.Visible = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            btnPause.Enabled = false;
            btnResume.Enabled = false;
            btnCancel.Enabled = false;
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtUrl.Text))
            {
                MessageBox.Show("Please enter a valid URL", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            downloadUrl = txtUrl.Text;

            uriString = new Uri(downloadUrl);
            if (Uri.IsWellFormedUriString(uriString.ToString(), UriKind.Absolute))
            {
                fileName = Path.GetFileName(uriString.LocalPath);
                lblFilename.Text = $"FileName: {fileName}";

                var fileExtension = Path.GetExtension(fileName);
                saveFileDialog.Filter = $"Files (*{fileExtension})|*{fileExtension}";
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.FileName = fileName;

                if (saveFileDialog.ShowDialog() != DialogResult.OK) return;
                savePath = saveFileDialog.FileName;
            }
            else
            {
                MessageBox.Show("Invalid URI");
            }

            Invoke((Action)(() =>
                lblFilesize.Text = $"File size: {GetFileSize(uriString)}"));

            SetNotificationIcon(DOWNLOAD_STARTED);
            SetNotificationText($"{Path.GetFileNameWithoutExtension(fileName)} is downloading..", DEFAULT_iCON);
            
            btnDownload.Enabled = false;
            btnPause.Enabled = true;
            btnCancel.Enabled = true;

            downloadThread = new Thread(DownloadFile);

            downloadThread.Start();
            Invoke((Action)(() => ControlBox = false));
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            isPaused = true;

            SetNotificationIcon(DOWNLOAD_PAUSED);
            SetNotificationText(DOWNLOAD_PAUSED, DEFAULT_iCON);

            btnPause.Enabled = false;
            btnResume.Enabled = true;
        }

        private void btnResume_Click(object sender, EventArgs e)
        {
            isPaused = false;

            SetNotificationIcon(DOWNLOAD_RESUMED);
            SetNotificationText(DOWNLOAD_RESUMED, DEFAULT_iCON);

            btnPause.Enabled = true;
            btnResume.Enabled = false;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("The download is in progress. Are you sure you want to cancel the download?", 
                "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                return;
            }

            Cancel();

            Invoke((Action)(() =>
            {
                ControlBox = true;
            }));
        }

        private async void DownloadFile()
        {
            ResetDownload();

            Invoke((Action)(() =>
            {
                jumper.Visible = true;
            }));

            try
            {
                webRequest = (HttpWebRequest)WebRequest.Create(downloadUrl);
                webRequest.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip");
                webRequest.AllowAutoRedirect = false;

                using (webResponse = (HttpWebResponse)await webRequest.GetResponseAsync())
                {
                    stream = webResponse.GetResponseStream();

                    if (webResponse.ContentEncoding.ToLower().Contains("gzip"))
                    {
                        if (stream != null)
                            stream = new System.IO.Compression.GZipStream(stream,
                                System.IO.Compression.CompressionMode.Decompress);
                    }

                    fileSize = webResponse.ContentLength;

                    fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write,
                        FileShare.None);

                    downloadSpeed = 0;
                    timeRemaining = TimeSpan.Zero;

                    buffer = new byte[4192 * 10];

                    startTime = DateTime.Now;

                    while (!isCancelled && !isdownloadInProgress)
                    {
                        if (isCancelled)
                        {
                            webRequest.Abort(); // If download is cancelled then cancel the web request
                            break;
                        }

                        if (isPaused)
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        if (stream != null)
                        {
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            bytesDownloaded += bytesRead;

                            if (bytesRead == 0)
                            {
                                break;
                            }
                        }

                        downloadProgress = (int)((double)bytesDownloaded / fileSize * 100);

                        UpdateUI();

                        UpdateTaskBar();

                        if (bytesDownloaded != fileSize)
                        {
                            continue;
                        }

                        if (bytesDownloaded == fileSize)
                        {
                            if (InvokeRequired)
                            {
                                Invoke((Action)(SetDownloadCompleted));
                            }
                        }
                        break;
                    }
                    downloadThread.Abort();
                    webResponse.Close();
                    fileStream.Close();

                    Invoke((Action)(() =>
                    {
                        ControlBox = true;
                        btnDownload.Enabled = true;
                        btnPause.Enabled = false;
                        btnCancel.Enabled = false;
                        jumper.Visible = false;
                    }));
                }
            }
            catch (WebException ex)
            {
                if (ex.Status != WebExceptionStatus.RequestCanceled)
                {
                    // It removes the web abort dialog! Is annoying. This happens when you cancel the download.
                    // Re-throw the exception if it is not a request canceled exception.
                    throw;
                }
                SetWebException(ex.Message);
            }
            catch (Exception ex)
            {
                Invoke((Action)(() =>
                {
                    btnDownload.Enabled = true;
                    btnPause.Enabled = false;
                    btnCancel.Enabled = false;
                    SetDownloadError(ex.Message);
                    isdownloadInProgress = true;
                    ControlBox = true;
                }));
            }
        }

        private void UpdateUI()
        {
            Invoke((Action)(() =>
                {
                    lblProgress.Text =
                        $"Progress: {downloadProgress}% ({FileSizeFormatter(bytesDownloaded)} / {FileSizeFormatter(fileSize)})";
                }));

                UpdateTaskBar();
                UpdateProgressBar(downloadProgress);
                UpdateDownloadSpeedAndTimeRemaining(startTime, bytesDownloaded);
        }

        private void UpdateTaskBar()
        {
            TaskbarItemInfo taskbarItemInfo = new TaskbarItemInfo
            {
                ProgressState = TaskbarItemProgressState.Normal,
                ProgressValue = 0.5
            };

            taskbarItemInfo.ProgressValue = downloadProgress;
        }

        private void UpdateProgressBar(int progress)
        {
            if (progressBar1.InvokeRequired)
            {
                Invoke((Action)(() =>
                {
                    UpdateProgressBar(progress);
                }));
            }
            else
            {
                if (progress < progressBar1.Minimum)
                {
                    progress = progressBar1.Minimum;
                }
                else if (progress > progressBar1.Maximum)
                {
                    progress = progressBar1.Maximum;
                }

                progressBar1.Value = progress;
            }
        }

        private void UpdateDownloadSpeedAndTimeRemaining(DateTime date, long bDownloaded)
        {
            // Check if the code is executing on the UI thread
            if (lblDownloadSpeed.InvokeRequired || lblTimeLeft.InvokeRequired)
            {
                Invoke((MethodInvoker)delegate
                {
                    UpdateDownloadSpeedAndTimeRemaining(date, bDownloaded);
                });
            }
            else
            {
                var elapsedTime = DateTime.Now - date;
                downloadSpeed = bDownloaded / elapsedTime.TotalSeconds;
                timeRemainingSeconds = (fileSize - bDownloaded) / downloadSpeed;

                if (timeRemainingSeconds > TimeSpan.MaxValue.TotalSeconds || double.IsNaN(timeRemainingSeconds))
                {
                    timeRemaining = TimeSpan.Zero; // Set timeRemaining to a default value
                }
                else
                {
                    timeRemaining = TimeSpan.FromSeconds(timeRemainingSeconds);
                }

                lblDownloadSpeed.Text = $"Download speed: {FileSizeFormatter(downloadSpeed)}";
                lblTimeLeft.Text = $"Time remaining: {timeRemaining:hh\\:mm\\:ss}";
            }
        }

        private string GetFileSize(Uri uriPath)
        {
            var request = WebRequest.Create(uriPath);
            request.Method = "HEAD";

            using (var response = request.GetResponse())
            {
                var contentLength = response.Headers.Get("Content-Length");
                var fileSizeInMegaByte = FileSizeFormatter(Convert.ToInt64(contentLength));
                return fileSizeInMegaByte;
            }
        }

        private string FileSizeFormatter(double sizeInBytes)
        {
            if (sizeInBytes >= 1024 * 1024 * 1024)
            {
                return $"{sizeInBytes / (1024.0 * 1024 * 1024):0.00} GB";
            }

            return sizeInBytes >= 1024 * 1024 ? $"{sizeInBytes / (1024.0 * 1024):0.00} MB" : $"{sizeInBytes / 1024.0:0.00} KB";
        }

        private void SetNotificationIcon(string iconType)
        {
            if (progressBar1.IsDisposed || progressBar1.InvokeRequired)
            {
                return;
            }

            switch (iconType)
            {
                case ICON_DEFAULT:
                    notifyIcon1.Icon = SystemIcons.Application;
                    break;
                case ICON_ERROR:
                    notifyIcon1.Icon = SystemIcons.Error;
                    break;
                case ICON_SUCCESS:
                    notifyIcon1.Icon = SystemIcons.Information;
                    break;
                case ICON_WARNING:
                    notifyIcon1.Icon = SystemIcons.Warning;
                    break;
            }
        }

        private void SetNotificationText(string text, ToolTipIcon iconInfo)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate
                {
                    notifyIcon1.Text = text;
                    notifyIcon1.ShowBalloonTip(3000, text, text, iconInfo);
                });
            }
            else
            {
                notifyIcon1.Text = text;
                notifyIcon1.ShowBalloonTip(3000, text, text, iconInfo);
            }
        }

        private void SetDownloadCompleted()
        {
            // Set the notification icon and text
            SetNotificationIcon(ICON_SUCCESS);
            SetNotificationText(DOWNLOAD_COMPLETED, SUCCESS_ICON);

            Invoke((Action)(() =>
            {
                lblTimeLeft.Text = "Time rmaining: ";
                lblDownloadSpeed.Text = "Download speed: ";
                lblFilename.Text = "File name: ";
                lblFilesize.Text = "File size: ";
                lblProgress.Text = "Progress: ";
            }));
        }

        private void SetDownloadCancelled()
        {
            SetNotificationIcon(DOWNLOAD_CANCELLED);
            SetNotificationText(DOWNLOAD_CANCELLED, WARNING_ICON);
        }

        private void SetDownloadError(string exception)
        {
            SetNotificationIcon(DOWNLOAD_ERROR);
            SetNotificationText($"{exception}", ERROR_ICON);

            //SetTaskbarProgress(TASKBAR_PROGRESS_ERROR);
        }

        private void SetWebException(string webException)
        {
            SetNotificationIcon(DOWNLOAD_ERROR);
            SetNotificationText($"{webException}", ERROR_ICON);

            //SetTaskbarProgress(TASKBAR_PROGRESS_ERROR);
        }
        
        private void ResetDownload()
        {
            Invoke((MethodInvoker)delegate
            {;
                fileSize = 0;
                bytesDownloaded = 0;
                downloadProgress = 0;
                downloadSpeed = 0;
                timeRemaining = TimeSpan.Zero;
                isCancelled = false;
                isPaused = false;
                isdownloadInProgress = false;
                progressBar1.Value = 0;
            });
        }

        private void Cancel()
        {
            isCancelled = true;
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate
                {
                    jumper.Visible = false;
                    ControlBox = true;
                });
            }
            else
            {
                ControlBox = true;
            }
            if (downloadThread != null && downloadThread.IsAlive)
            {
                downloadThread.Abort();
            }

            jumper.Visible = false;
            // Cancel the download and close the connection
            webRequest.Abort();
            webResponse.Close();
            stream.Close();
            fileStream.Close();

            // Reset the variables and progress bar to their initial state
            isCancelled = false;
            isPaused = false;
            bytesDownloaded = 0;
            downloadProgress = 0;
            progressBar1.Value = downloadProgress;

            // Set the notification icon and text
            SetDownloadCancelled();
            
            lblTimeLeft.Text = "Time rmaining: ";
            lblDownloadSpeed.Text = "Download speed: ";
            lblFilename.Text = "File name: ";
            lblFilesize.Text = "File size: ";
            lblProgress.Text = "Progress: ";
            
            btnDownload.Invoke((MethodInvoker)delegate { btnDownload.Enabled = true; });
            btnPause.Invoke((MethodInvoker)delegate { btnPause.Enabled = false; });
            btnResume.Invoke((MethodInvoker)delegate { btnResume.Enabled = false; });
            btnCancel.Invoke((MethodInvoker)delegate { btnCancel.Enabled = false; });
        }
    }
}
