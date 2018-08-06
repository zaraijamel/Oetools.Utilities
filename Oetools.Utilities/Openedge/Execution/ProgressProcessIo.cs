﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProgressProcessIo.cs) is part of Oetools.Utilities.
// 
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    /// Represents a progres process
    /// </summary>
    /// <remarks>
    ///     - progress returns an exit different of 0 only if it actually failed to start,
    ///     if your procedure return error or quit, it is still an exit code of 0
    ///     - in batch mode (-b) and GUI mode, even if we set CreateNoWindow and WindowStyle to Hidden,
    ///     the window still appears in the taskbar. All the code between #if WINDOWSONLYBUILD in this class
    ///     is made to hide this window from the taskbar in that case
    /// </remarks>
    public class ProgressProcessIo : ProcessIoAsync {
        
#if WINDOWSONLYBUILD
        private Timer _timer;
#endif
        
        /// <summary>
        /// DLC path to use
        /// </summary>
        public string DlcPath { get;  }
        
        /// <summary>
        /// Whether or not to use character mode (_progres) instead of GUI (prowin)
        /// </summary>
        public bool UseCharacterMode { get; }

        /// <summary>
        /// Whether or not the executable can use the -nosplash parameter
        /// </summary>
        public bool? CanUseNoSplash { get; set; }
        
        /// <summary>
        /// The complete start parameters used
        /// </summary>
        public string StartParameters { get; private set; }

        private StringBuilder _batchModeOutput;
        
        /// <summary>
        /// Returns all the messages send by progres to the standard output, should be used once the process has exited
        /// </summary>
        /// <remarks>we also append the error output but it seems that _progres always outputs to the standard output, even errors</remarks>
        public StringBuilder BatchModeOutput {
            get {
                if (_batchModeOutput == null || _process != null && !_process.HasExited) {
                    _batchModeOutput = new StringBuilder();
                    foreach (var s in ErrorOutputArray) {
                        _batchModeOutput.Append(s);
                    }
                    foreach (var s in StandardOutputArray) {
                        _batchModeOutput.Append(s);
                    }
                }
                return _batchModeOutput;
            }
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        public ProgressProcessIo(string dlcPath, bool useCharacterModeOfProgress, bool? canUseNoSplash = null) : base (null) {
            DlcPath = dlcPath;
            UseCharacterMode = useCharacterModeOfProgress;
            CanUseNoSplash = canUseNoSplash;
            ExecutablePath = ProUtilities.GetProExecutableFromDlc(DlcPath, UseCharacterMode);
        }
        
#if WINDOWSONLYBUILD
        public new void Dispose() {
            base.Dispose();
            _timer?.Dispose();
            _timer = null;
        }
#endif
        
        protected override void ExecuteAsyncProcess(string arguments = null, bool silent = true) {
            base.ExecuteAsyncProcess(arguments, silent);
#if WINDOWSONLYBUILD
            if (silent && !UseCharacterMode) {
                _timer = new Timer {
                    Interval = 100,
                    AutoReset = true
                };
                _timer.Elapsed += TimerOnElapsed;
                _timer.Start();
            }
#endif
        }

        protected override void WaitUntilProcessExits(int timeoutMs) {
            base.WaitUntilProcessExits(timeoutMs);
            RestoreSplashScreen();
        }

        protected override void PrepareStart(string arguments, bool silent) {
            
            if (silent) {
                arguments = $"{arguments ?? ""} -b";
            }
            
            if (!UseCharacterMode) {
                if (CanUseNoSplash != null && CanUseNoSplash.Value || ProUtilities.CanProVersionUseNoSplashParameter(ProUtilities.GetProVersionFromDlc(DlcPath))) {
                    arguments = $"{arguments ?? ""} -nosplash";
                } else {
                    DisableSplashScreen();
                }
            }

            StartParameters = arguments;

            // we can only redirect output in -b batch mode 
            RedirectOutput = silent;
            
            base.PrepareStart(arguments, silent);

            // in character mode, we need to execute _progress in a console!
            if (UseCharacterMode && !silent) {
                _startInfo.UseShellExecute = true;
            }
        }

        protected override void ProcessOnExited(object sender, EventArgs e) {
            base.ProcessOnExited(sender, e);
            RestoreSplashScreen();
        }

        private void DisableSplashScreen() {
            try {
                File.Move(Path.Combine(DlcPath, "bin", "splashscreen.bmp"), Path.Combine(DlcPath, "bin", "splashscreen-disabled.bmp"));
            } catch (Exception) {
                // if it fails it is not really a problem
            }
        }

        private void RestoreSplashScreen() {
            try {
                File.Move(Path.Combine(DlcPath, "bin", "splashscreen-disabled.bmp"), Path.Combine(DlcPath, "bin", "splashscreen.bmp"));
            } catch (Exception) {
                // if it fails it is not really a problem
            }
        }

#if WINDOWSONLYBUILD
        
        private void TimerOnElapsed(object sender, ElapsedEventArgs e) {
            Debug.WriteLine("timer tick");
            if (HideProcessFromTaskBar(_process.Id) || _process.TotalProcessorTime.TotalMilliseconds > 1500) {
                Debug.WriteLine("timer stopped");
                _timer.Dispose();
            }
        }
        
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className,  string windowTitle);

        [DllImport("user32.dll", SetLastError=true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        // ReSharper disable once InconsistentNaming
        private const int GWL_EX_STYLE = -20;

        // ReSharper disable once InconsistentNaming
        private const int WS_EX_APPWINDOW = 0x00040000;

        // ReSharper disable once InconsistentNaming
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        
        private bool HideProcessFromTaskBar(int procId) {
            var hWnd = IntPtr.Zero;
            do {
                hWnd = FindWindowEx(IntPtr.Zero, hWnd, OeConstants.ProwinWindowClass, null);
                GetWindowThreadProcessId(hWnd, out var hWndProcessId);
                if (hWndProcessId == procId) {
                    SetWindowLong(hWnd, GWL_EX_STYLE, (GetWindowLong(hWnd, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
                    return true;
                }
            } while(hWnd != IntPtr.Zero);	
            return false;
        }

#endif
    }
}