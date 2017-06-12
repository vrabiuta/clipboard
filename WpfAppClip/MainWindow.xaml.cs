using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WpfAppClip
{
    //https://csharphardcoreprogramming.wordpress.com/tag/clipboard/
    //https://stackoverflow.com/questions/2886756/trigger-os-to-copy-ctrlc-or-ctrl-x-programmatically

    public partial class MainWindow : Window
    {
        private bool stopped;
        Stopwatch sw = new Stopwatch();
        private CliboardMonitor cliboardMonitor;
        private bool monitorActive;
        public MainWindow()
        {
            InitializeComponent();

            
            Unloaded += (sender, args) =>
            {
                stopped = true;
            };

            

            Loaded += (sender, args) =>
            {

                cliboardMonitor = new CliboardMonitor(this, s =>
                {
                    if (!monitorActive) return;
                    sw.Stop();
                    Debug.WriteLine("CLIPBOARD: " + s + " " + sw.Elapsed.TotalMilliseconds + "ms");
                    monitorActive = false;
                });

                Task.Factory.StartNew(() =>
                {
                    while (!stopped)
                    {
                        Thread.Sleep(5000);

                        sw.Reset();
                        sw.Start();
                        monitorActive = true;
                        Keyboard.SimulateKeyStroke('c', ctrl: true);
                        Debug.WriteLine("KEYBOARD: Ctrl+C");
                    }
                });
            };
        }

        
        
        

        

    } // class

    public class CliboardMonitor : IDisposable
    {
        private readonly Window _wnd;
        private readonly Action<string> _onChange;

        private const int WM_DRAWCLIPBOARD = 0x0308; // change notifications
        private const int WM_CHANGECBCHAIN = 0x030D; // another window is removed from the clipboard viewer chain
        private const int WM_CLIPBOARDUPDATE = 0x031D; // clipboard changed contents

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetClipboardViewer(IntPtr xHWndNewViewer);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool ChangeClipboardChain(IntPtr xHWndRemove, IntPtr xHWndNewNext);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr xHWnd, int xMessage, IntPtr xWParam, IntPtr xLParam);

        private IntPtr _HWndNextViewer; // next window
        private HwndSource _HWndSource; // this window
        private string _Text = string.Empty;

        public CliboardMonitor(Window wnd, Action<string> onChange)
        {
            _wnd = wnd;
            _onChange = onChange;
            StartListeningToClipboard();
        }

        private void StartListeningToClipboard()
        {
            var lWindowInteropHelper = new WindowInteropHelper(_wnd);
            _HWndSource = HwndSource.FromHwnd(lWindowInteropHelper.Handle);
            _HWndSource.AddHook(WinProc);
            _HWndNextViewer = SetClipboardViewer(_HWndSource.Handle);   // set this window as a viewer
        } //

        private void StopListeningToClipboard()
        {
            ChangeClipboardChain(_HWndSource.Handle, _HWndNextViewer); // remove from cliboard viewer chain
            _HWndNextViewer = IntPtr.Zero;
            _HWndSource.RemoveHook(WinProc);
        } //


        private IntPtr WinProc(IntPtr xHwnd, int xMessageType, IntPtr xWParam, IntPtr xLParam, ref bool xHandled)
        {
            switch (xMessageType)
            {
                case WM_CHANGECBCHAIN:
                    if (xWParam == _HWndNextViewer) _HWndNextViewer = xLParam;
                    else if (_HWndNextViewer != IntPtr.Zero) SendMessage(_HWndNextViewer, xMessageType, xWParam, xLParam);
                    break;

                case WM_DRAWCLIPBOARD:
                    SendMessage(_HWndNextViewer, xMessageType, xWParam, xLParam);

                    processWinProcMessage();
                    break;
            }

            return IntPtr.Zero;
        } //

        private void processWinProcMessage()
        {
            if (!Clipboard.ContainsText()) return;
            string lPreviousText = _Text;
            _Text = Clipboard.GetText();
            if (_Text.Equals(lPreviousText)) return; // do not play the same text again

            _onChange(_Text);
        } //
        public void Dispose()
        {
            StopListeningToClipboard();
        }
    }
}
