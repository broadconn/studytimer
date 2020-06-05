using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;

namespace WpfApp1 {
    public delegate void TimerEndsEventHandler();
    public delegate void QuitRequestedEventHandler();

    public partial class MainWindow : Window {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        bool alwaysOnTop = true;
        bool progressRunning = false;
        bool paused = false;  
        Thread progThread;
        Color startCol = Colors.White;
        Color endCol = Color.FromArgb(1, 50, 50, 50);
        DateTime lastTimeThreadSlept; 
        bool internetEnabled = false; 
        Random rand = new Random();
        bool mouseInside = false;

        enum Stage { Recall, WarmUp, Study, Original }
        Stage curStage = Stage.Recall;

        enum StudyMode { Study, Break };
        StudyMode mode = StudyMode.Study;

        //timer things
        int totalStudySeconds, totalBreakSeconds;
        List<string> lstSubjects;
        List<float> percToggles = new List<float>();


        List<DispatcherTimer> alertTimers = new List<DispatcherTimer>();

        public delegate void StudyEnd();
        public event StudyEnd OnStudyEnd; 

        public MainWindow() {
            InitializeComponent(); 
            UpdateHeight();

            ProgressGrid.Visibility = Visibility.Hidden; 
            hidePanel.Margin = new Thickness(0, 0, 0, 0);

            //toggle btn colors
            UpdateBtnCols();
        }

        public void SetData(int totalStudySeconds, int breakSeconds, List<string> subjects, bool disableNet) {
            //study length, break length, list<string> subjects
            this.totalStudySeconds = totalStudySeconds;
            this.totalBreakSeconds = breakSeconds;
            this.lstSubjects = subjects;
            internetEnabled = !disableNet;

            mode = StudyMode.Study;

            //set up progress toggles
            int numBreaks = subjects.Count - 1;//e.g.3 subjects = 2 breaks
            for (int i = 0; i < numBreaks; i++) {
                float start = (i + 1) * (totalStudySeconds / (float)subjects.Count) - (totalBreakSeconds / 2);
                float end = (i + 1) * (totalStudySeconds / (float)subjects.Count) + (totalBreakSeconds / 2);
                percToggles.Add(start / (float)totalStudySeconds);
                percToggles.Add(end / (float)totalStudySeconds);
            }
            if (numBreaks == 0) percToggles.Add(1f);

            UpdateBtnCols();
        }

        float GetPercThroughCurSubject(float totalPerc) {
            if(totalPerc < percToggles[0]) { 
                return totalPerc  / percToggles[0];
            }
            else if (totalPerc > percToggles[percToggles.Count - 1]) {
                return (totalPerc - percToggles[percToggles.Count - 1]) / (1f - percToggles[percToggles.Count - 1]); 
            }

            for(int i = 0; i < percToggles.Count-1; i++) 
                if(totalPerc > percToggles[i] && totalPerc < percToggles[i + 1]) 
                    return (totalPerc - percToggles[i]) / (percToggles[i + 1] - percToggles[i]); 

            return 0;
        }

        public void Run() {
            //begin! 
            progressRunning = true;

            //disable internet if turned off
            if (!internetEnabled)
                EnableInternet(false);

            //window height
            UpdateHeight();

            //dividers
            int numDivs = 3;
            CreateDividers(numDivs); 

            //start the progress run
            ProgressGrid.Visibility = Visibility.Visible;

            //timer vars
            DateTime startTime = DateTime.Now;
            DateTime alertStartTime = DateTime.Now;
            int totalSeconds = totalStudySeconds;

            //set string 
            lblStudy.Content = lstSubjects[0];

            //Set up threads and GO 
            Progress<int> progress = new Progress<int>(value => progressBar.Value = value);
            if (progThread != null && progThread.IsAlive) progThread.Abort();
            progThread = new Thread(() => {
                float timePassed = 0;
                float alertTimeInc = totalSeconds / (float)numDivs;
                float nextAlertTime = alertTimeInc;

                float subjPercLastFrame = 0;
                bool isSubject = true;
                int curSubjectIdx = 0;

                paused = false;

                while (timePassed < totalSeconds) {
                    //if paused, add deltaTime to 
                    if (paused) {
                        double deltaTme = (DateTime.Now - lastTimeThreadSlept).TotalSeconds;
                        startTime = startTime.AddSeconds(deltaTme);
                    }

                    //get time passed
                    TimeSpan runSpan = DateTime.Now - startTime;
                    timePassed = (float)runSpan.TotalSeconds;

                    //total perc passed
                    float percPassed = timePassed / (float)totalSeconds;
                    float percPassedThisSubject = GetPercThroughCurSubject(percPassed);
                    //Console.WriteLine("perc: " + percPassedThisSubject.ToString("n2") + " time: " + timePassed.ToString("n2"));

                    //update text when subject / break changes
                    if(subjPercLastFrame - percPassedThisSubject > 0.8f) {
                        isSubject = !isSubject;
                        if (isSubject) curSubjectIdx++;

                        //if in break, enable net. If resuming study, put net back to desrired setting.
                        bool inbreak = InBreak(percPassed);
                        if (inbreak) EnableInternet(true);
                        else EnableInternet(internetEnabled);

                        //update visuals, set progress text 
                        Dispatcher.Invoke(new Action(() => {
                            MinHeight = isSubject ? 35 : 25;
                            lblStudy.Content = isSubject ? lstSubjects[curSubjectIdx] : "Break! Next up: " + lstSubjects[curSubjectIdx+1];
                        }), DispatcherPriority.ContextIdle);   
                    }

                    //progress bar  
                    Dispatcher.Invoke(new Action(() => { 
                        //progress bar size
                        double rightMargin = (txtProg.ActualWidth * (1d - percPassedThisSubject)).Clamp(1, (txtProg.ActualWidth)); //bar right edge position
                        progFillRect.Margin = new Thickness(progFillRect.Margin.Left, progFillRect.Margin.Top, rightMargin, progFillRect.Margin.Bottom); //progress fill  

                        //progress bar color
                        float percThruCurAlert = ((timePassed % alertTimeInc) / alertTimeInc); 
                        progressBar.Background = new SolidColorBrush(paused ? Colors.Orange : Color.FromRgb(
                            (byte)Math.Round(startCol.R * (1 - percThruCurAlert) + endCol.R * percThruCurAlert),
                            (byte)Math.Round(startCol.G * (1 - percThruCurAlert) + endCol.G * percThruCurAlert),
                            (byte)Math.Round(startCol.B * (1 - percThruCurAlert) + endCol.B * percThruCurAlert)
                            ));

                        //hide panel color
                        hidePanel.Background = new SolidColorBrush(InBreak(percPassed) ? Colors.Orange : Color.FromRgb(
                            (byte)Math.Round(startCol.R * (1 - percPassedThisSubject) + endCol.R * percPassedThisSubject),
                            (byte)Math.Round(startCol.G * (1 - percPassedThisSubject) + endCol.G * percPassedThisSubject),
                            (byte)Math.Round(startCol.B * (1 - percPassedThisSubject) + endCol.B * percPassedThisSubject)
                            ));
                    }), DispatcherPriority.ContextIdle); //set progress text    

                    lastTimeThreadSlept = DateTime.Now;
                    subjPercLastFrame = percPassedThisSubject;
                    Thread.Sleep(100);
                }

                //end of timer, reset stuff
                Dispatcher.Invoke(new Action(() => { //dispatcher because this isnt main thread
                    TimerEnded();
                }), DispatcherPriority.ContextIdle); //set start button text 
            });
            progThread.Start();
        }

        bool InBreak(float percPassed) {
            bool inbreak = false;
            foreach (float f in percToggles)
                if (f > percPassed) break;
                else inbreak = !inbreak; 
            return inbreak;
        }

        void UpdateBtnCols() {
            btnAlwaysTop.Background = new SolidColorBrush(alwaysOnTop ? Colors.Black : Colors.Gray);
            btnInternet.Background = new SolidColorBrush(internetEnabled ? Colors.Gray : Colors.DodgerBlue);
        } 

        void UpdateHeight() {
            float minVal = 25f;
            float maxVal = 45f; 

            if (progressRunning && !mouseInside && TheWindow.WindowState == WindowState.Maximized) { 
                minVal = 15;
            }

            TheWindow.MinHeight = progressRunning ? minVal : maxVal;
            TheWindow.Height = progressRunning ? minVal : maxVal;
            TheWindow.MaxHeight = progressRunning ? minVal : maxVal;
        }  

        void TimerEnded() {  
            RunEnded();
        }

        void RunEnded() {
            if (progThread != null) { 
                progThread.Abort();
            } 
            EnableInternet(true); //enable internet
              
            BGPanel.Background = new SolidColorBrush(Colors.GhostWhite);
            TimerFinished();  
            OnStudyEnd?.Invoke();
            Close();
        }   

        void TimerFinished() {
            Dispatcher.Invoke(new Action(() => {
                ProgressGrid.Visibility = Visibility.Hidden; 
                progressRunning = false;
                UpdateHeight();
                WindowState = WindowState.Normal;
            }), DispatcherPriority.ContextIdle);
        }

        void CreateDividers(int numbAlerts) {
            Columnz.ColumnDefinitions.Clear(); 

            for (int i = 0; i < numbAlerts; i++) {
                ColumnDefinition colDef1 = new ColumnDefinition();
                Columnz.ColumnDefinitions.Add(colDef1);

                Button b = new Button();
                b.Content = "";
                b.HorizontalAlignment = HorizontalAlignment.Right;
                b.Width = 7;
                b.Background = new SolidColorBrush(Colors.White);
                Grid.SetColumn(b, i);
                Columnz.Children.Add(b);

                Button b2 = new Button();
                b2.Content = "";
                b2.HorizontalAlignment = HorizontalAlignment.Right;
                b2.Width = 5;
                b2.Background = new SolidColorBrush(Colors.Black);
                Grid.SetColumn(b2, i); 
                Columnz.Children.Add(b2); 
            }
        }

        void SetupAlertEvents(int totalSeconds, int numAlerts) {
            float timeInc = totalSeconds / numAlerts;
            float timeSet = 0;

            for (int i = 0; i < numAlerts; i++) {
                timeSet += timeInc;

                DispatcherTimer dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                dispatcherTimer.Interval = TimeSpan.FromSeconds(timeSet);
                dispatcherTimer.Start();
                alertTimers.Add(dispatcherTimer);
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e) {
        }

        void SetBG() {
            TheWindow.Background = new SolidColorBrush(Colors.Red);
        }

        private void Window_Deactivated(object sender, EventArgs e) {
            this.Topmost = alwaysOnTop;
        }

        private void Quit(object sender, RoutedEventArgs e) { 
            RunEnded();
        }

        private void ToggleAlwaysOnTop(object sender, RoutedEventArgs e) {
            if(WindowState == WindowState.Normal) WindowState = WindowState.Maximized;
            else WindowState = WindowState.Normal;
            UpdateBtnCols();
        }

        private void TheWindow_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void cmbAlerts_SelectionChanged(object sender, SelectionChangedEventArgs e) { 
        }

        private void PauseBtn_Click(object sender, RoutedEventArgs e) {
            paused = !paused;
        }  

        void EnableInternet(bool enable)
        {
            string filePath = "C:\\Windows\\System32\\drivers\\etc\\hosts";

            //open hosts file
            /*List<string> hostsLines = new List<string>();
            try
            {
                //Pass the file path and file name to the StreamReader constructor
                StreamReader sr = new StreamReader(filePath);

                //Read the first line of text
                string line = sr.ReadLine(); 
                //Continue to read until you reach end of file
                while (line != null)
                {
                    //write the lie to console window
                    Console.WriteLine(line);
                    hostsLines.Add(line);
                    //Read the next line
                    line = sr.ReadLine();
                } 
                //close the file
                sr.Close(); 
            }
            catch (Exception e) { Console.WriteLine("Exception: " + e.Message); }


            //write to hosts
            try
            { 
                //Pass the filepath and filename to the StreamWriter Constructor
                StreamWriter sw = new StreamWriter(filePath);

                for(int i = 0; i < hostsLines.Count; i++)
                {
                    sw.WriteLine(hostsLines[i]);
                } 

                //Close the file
                sw.Close();
            }
            catch (Exception e) { Console.WriteLine("Exception: " + e.Message); } */

            string str = enable ? "renew" : "release";
            ProcessStartInfo internet = new ProcessStartInfo() {
                FileName = "cmd.exe",
                Arguments = "/C ipconfig /" + str,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(internet);
        }

        private void BtnInternet_Click(object sender, RoutedEventArgs e) {
            //ToggleInternetSetting();
        }

        private void TheWindow_MouseEnter(object sender, MouseEventArgs e) {
            mouseInside = true;
            UpdateHeight(); 
        }

        private void TheWindow_MouseLeave(object sender, MouseEventArgs e) {
            mouseInside = false;
            UpdateHeight(); 
        }

        private void TheWindow_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateHeight();
        } 

        private void hidePanel_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                hidePanel.Visibility = Visibility.Hidden;
            }
        }

        private void progFillRect_MouseDown(object sender, MouseButtonEventArgs e) {
            hidePanel.Visibility = Visibility.Visible;
        }

        private void ProgressGrid_MouseDown(object sender, MouseButtonEventArgs e) {
            hidePanel.Visibility = Visibility.Visible;
        }

        private void ResetProgress_Click(object sender, RoutedEventArgs e) {
            //reset progress 
        }
    }
}

public struct TimerSettings {
    public string text;
    public TimeSpan span;
    public int numDividers;
    public List<string> options; 
}

public class CountingProgressBar : ProgressBar {
    Thread timerThread;
    public CountingProgressBar(int Time) {
        Maximum = Time;
        SetTimer(Time);
    }

    void SetTimer(int time) {
        if (timerThread != null && timerThread.IsAlive)
            timerThread.Abort();

        timerThread = new Thread(() => {
            for (int i = 0; i < time; ++i) {
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate () {
                    Value += 1;
                }));
                Thread.Sleep(1000);
            }

            ProgressComplete();
        });
        timerThread.IsBackground = true;
    }

    public void Start() {
        if (timerThread != null)
            timerThread.Start();
    }
    public void SetTime(int Time) {
        Maximum = Time;
        SetTimer(Time);
    }
    public void Reset() {
        Value = 0;
        SetTimer(Convert.ToInt32(Maximum));
    }

    public delegate void ProgressCompleted();
    public event ProgressCompleted ProgressCompletedEvent;
    private void ProgressComplete() {
        ProgressCompletedEvent?.Invoke();
    }
}

static class MathExtensions {
    public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T> {
        if (val.CompareTo(min) < 0) return min;
        else if (val.CompareTo(max) > 0) return max;
        else return val;
    }
}
