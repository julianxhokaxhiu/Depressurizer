﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Depressurizer.Core.Helpers;
using Depressurizer.Properties;
using System.ComponentModel;

namespace Depressurizer.Dialogs
{
    public partial class CancelableDialog : Form
    {
        #region Fields

        protected readonly object SyncRoot = new object();

        private int runningThreads;

        #endregion

        #region Constructors and Destructors

        public CancelableDialog(string title, bool stopButton)
        {
            InitializeComponent();

            Text = title;

            ButtonStop.Enabled = ButtonStop.Visible = stopButton;

            Stopped = false;
            Canceled = false;
            Threads = new List<Thread>();
            TotalJobs = 1;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool SingleThreadMode { get; set; }

        #endregion

        #region Delegates

        private delegate void SimpleDelegate();

        private delegate void TextUpdateDelegate(string s);

        #endregion

        #region Public Properties

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Exception Error { get; protected set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int JobsCompleted { get; protected set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public sealed override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        public ICollection<Thread> Threads { get; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TotalJobs { get; protected set; }

        #endregion

        #region Properties

        protected static Logger Logger => Logger.Instance;

        protected bool Canceled { get; private set; }

        protected bool Stopped { get; set; }

        #endregion

        #region Methods

        protected virtual void CancelableDialog_Load(object sender, EventArgs e)
        {
            Thread thread;
            int numberOfThreads = Math.Min(TotalJobs, Environment.ProcessorCount);
            if (SingleThreadMode)
                numberOfThreads = 1;
            for (int i = 0; i < numberOfThreads; i++)
            {
                thread = new Thread(RunProcessChecked);
                Threads.Add(thread);

                thread.Start();
                runningThreads++;
            }

            thread = new Thread(CheckClose)
            {
                IsBackground = true
            };
            thread.Start();

            UpdateText();
        }

        protected void DisableAbort()
        {
            if (InvokeRequired)
            {
                Invoke(new SimpleDelegate(DisableAbort));
            }
            else
            {
                ButtonStop.Enabled = ButtonCancel.Enabled = false;
            }
        }

        protected virtual void Finish() { }

        protected void OnJobCompletion()
        {
            lock (SyncRoot)
            {
                JobsCompleted++;
            }

            UpdateText();
        }

        protected void OnThreadCompletion()
        {
            if (InvokeRequired)
            {
                Invoke(new SimpleDelegate(OnThreadCompletion));
            }
            else
            {
                runningThreads--;
                Logger.Info("CancelableDlg:{0} | Thread completed, still running {1}.", Text, runningThreads);
            }
        }

        protected virtual void RunProcess() { }

        protected void SetText(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new TextUpdateDelegate(SetText), text);
            }
            else
            {
                lblText.Text = text;
            }
        }

        protected virtual void UpdateText() { }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            lock (SyncRoot)
            {
                Stopped = true;
                Canceled = true;
            }

            DisableAbort();
        }

        private void ButtonStop_Click(object sender, EventArgs e)
        {
            lock (SyncRoot)
            {
                Stopped = true;
            }

            DisableAbort();
        }

        private void CancelableDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            lock (SyncRoot)
            {
                Stopped = true;
            }

            DisableAbort();

            Logger.Info("Waiting on threads to exit...");
            foreach (Thread t in Threads)
            {
                t.Join();
            }

            Logger.Info("All threads have exited...");

            Finish();
            if (JobsCompleted >= TotalJobs)
            {
                DialogResult = DialogResult.OK;
            }
            else if (Canceled)
            {
                DialogResult = DialogResult.Cancel;
            }
            else
            {
                DialogResult = DialogResult.Abort;
            }
        }

        private void CheckClose()
        {
            while (runningThreads > 0)
            {
                Thread.Sleep(500);
            }

            if (InvokeRequired)
            {
                Invoke(new SimpleDelegate(Close));
            }
            else
            {
                Close();
            }
        }

        private void RunProcessChecked()
        {
            try
            {
                RunProcess();
            }
            catch (Exception e)
            {
                lock (SyncRoot)
                {
                    Stopped = true;
                    Error = e;
                }

                Logger.Warn("CancelableDlg:{0} | Thread threw an exception: {1}.", Text, e);

                DisableAbort();
                SetText(Resources.CancelableDialog_ThreadErrorStopping);

                OnThreadCompletion();
            }
        }

        #endregion
    }
}
