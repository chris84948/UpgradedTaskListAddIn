using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using MVVMLibrary;

namespace UpgradedTaskList
{
    class TaskListControlViewModel : ObservableObject
    {
        private EnvDTE80.DTE2 applicationObject;
        private String openFile;
        private TaskListItemCollectionViewModel _taskViewModel;
        private CommentTokenViewModel _tokenViewModel;
        private int _selectedScopeIndex;
        private String _selectedToken;

        /// <summary>
        /// Enum representing the different versions of scope of tasks being filtered
        /// </summary>
        public enum taskScope
        {
            Solution = 0,
            Project = 1,
            Class = 2
        }

        /// <summary>
        /// View model for the task item collection
        /// </summary>
        public TaskListItemCollectionViewModel TaskViewModel
        {
            get { return _taskViewModel; }
            set { _taskViewModel = value; }
        }

        /// <summary>
        /// View model for the tokens available
        /// </summary>
        public CommentTokenViewModel TokenViewModel
        {
            get { return _tokenViewModel; }
            set { _tokenViewModel = value; }
        }

        /// <summary>
        /// Index of the selected scope combobox on the GUI
        /// </summary>
        public int SelectedScopeIndex
        {
            get { return _selectedScopeIndex; }
            set
            {
                _selectedScopeIndex = value;
                OnPropertyChanged("SelectedScopeIndex");
            }
        }

        /// <summary>
        /// String object of the current selected token in the GUI
        /// </summary>
        public String SelectedToken
        {
            get { return _selectedToken; }
            set
            {
                _selectedToken = value;
                OnPropertyChanged("SelectedToken");
            }
        }

        /// <summary>
        /// Constructor, creates each sub view model (one for tasks and one for tokens)
        /// Also initializes the two comboboxes to default values
        /// </summary>
        /// <param name="applicationObject">Reference to the main application object</param>
        public TaskListControlViewModel(ref EnvDTE80.DTE2 applicationObject)
        {
            // Copy object reference locally
            this.applicationObject = applicationObject;

            // Set default values for the two comboboxes
            SelectedScopeIndex = (int)taskScope.Solution;
            SelectedToken = "ALL";

            // Instantiate each view mode
            TaskViewModel = new TaskListItemCollectionViewModel(ref applicationObject, SelectedToken, SelectedScopeIndex);
            TokenViewModel = new CommentTokenViewModel(ref applicationObject);

            // Read the current open file on startup
            openFile = TaskViewModel.GetCurrentFile();
        }

        /// <summary>
        /// Filters task list based on the new options selected from the GUI
        /// </summary>
        internal void RefilterTaskList()
        {
            if (SelectedToken == null) return;

            TaskViewModel.CreateFilteredTaskCollection(SelectedToken, SelectedScopeIndex);
        }

        /// <summary>
        /// Filters task list when the current open file changes
        /// </summary>
        internal void RefilterTaskListOnFileChange()
        {
            if (SelectedToken == null) return;

            // Check to see if the file has changed. If it has, update the variable and call method to refilter
            if (!openFile.Equals(TaskViewModel.GetCurrentFile()))
            {
                openFile = TaskViewModel.GetCurrentFile();
                TaskViewModel.CreateFilteredTaskCollection(SelectedToken, SelectedScopeIndex);
            }
        }

        #region Commands

        /// <summary>
        /// Command for adding a token button the GUI
        /// Token must have only alphanumeric characters, _, $, ( or )
        /// </summary>
        public ICommand AddToken { get { return new RelayCommand(AddTokenExecute, CanAddTokenExecute); } }

        private void AddTokenExecute()
        {
            // Open the token editor window
            TokenEditor tokenEditor = new TokenEditor();
            tokenEditor.ShowDialog();

            // If the OK button was pressed, add the token to the list
            if (tokenEditor.DialogResult == true)
            {
                TokenViewModel.AddToken(tokenEditor.Token);
            }

            // Select the new token from the list
            SelectedToken = tokenEditor.Token;

            // Force update of tokens
            TaskViewModel.GetTasksFromApplicationObject();
            TaskViewModel.CreateFilteredTaskCollection(SelectedToken, SelectedScopeIndex);
        }

        private bool CanAddTokenExecute()
        {
            return true;
        }

        /// <summary>
        /// Command for adding a token button the GUI
        /// Token must have only alphanumeric characters, _, $, ( or )
        /// </summary>
        public ICommand RemoveToken { get { return new RelayCommand(RemoveTokenExecute, CanRemoveTokenExecute); } }

        private void RemoveTokenExecute()
        {
            // Add the new token to the list and control
            TokenViewModel.RemoveToken(SelectedToken);

            // Select the default "ALL" token from the list
            SelectedToken = "ALL";

            // Force update of tokens
            TaskViewModel.GetTasksFromApplicationObject();
            TaskViewModel.CreateFilteredTaskCollection(SelectedToken, SelectedScopeIndex);
        }

        private bool CanRemoveTokenExecute()
        {
            switch (SelectedToken)
            {
                case "ALL":
                    return false;

                case "TODO":
                    return false;

                case "HACK":
                    return false;

                case "UNDONE":
                    return false;

                default:
                    return true;
            }
        }

        #endregion

        #region SolutionEvents

        /// <summary>
        /// Called when a new solution is opened
        /// </summary>
        public void solutionEvents_Opened()
        {
            if (toolWindowControl == null) return;

            // Reset the task update timer - it takes a small delay from opening solution to task list being updated
            solutionOpenedDelayTimer.Reset();

            // Reset the retry count - we want to re-read this a number of times to allow for larger projects opening
            retrySolutionOpenedCount = 0;
            currentListCount = 0;
        }

        /// <summary>
        /// Called just before the current solution closes
        /// </summary>
        public void solutionEvents_BeforeClosing()
        {
            if (toolWindowControl == null) return;

            // Call solution closing on control
            toolWindowControl.SolutionClosing_EventHandler();
        }

        /// <summary>
        /// Called when the current open window changes in the editor
        /// </summary>
        /// <param name="GotFocus">Window that has focus</param>
        /// <param name="LostFocus">Window that lost focus</param>
        private void windowEvents_WindowActivated(Window GotFocus, Window LostFocus)
        {
            if (toolWindowControl == null) return;

            // Call new document opened on control
            toolWindowControl.WindowActivated_EventHandler();
        }

        /// <summary>
        /// Called whenever any line is changed in the current text document
        /// </summary>
        /// <param name="StartPoint"></param>
        /// <param name="EndPoint"></param>
        /// <param name="Hint"></param>
        private void textEditorEvents_LineChanged(TextPoint StartPoint, TextPoint EndPoint, int Hint)
        {
            if (toolWindowControl == null) return;

            taskUpdateDelayTimer.Reset();
        }

        #endregion

        #region Timers

        /// <summary>
        /// This event is waiting for the internal task list to update then
        /// updates and filters the add-on list
        /// </summary>
        private void taskUpdateTimer_Elapsed(object source, ElapsedEventArgs e)
        {
            toolWindowControl.ReadAllTasksAndFilter();
        }

        /// <summary>
        /// When the solution is opened, it can take a long timer before the tasks are read through
        /// This timer will try to reread all the tasks. If there are no tasks found, it will retry
        /// for a certain amount of timer before giving up
        /// </summary>
        private void solutionOpenedTimer_Elapsed(object source, ElapsedEventArgs e)
        {
            toolWindowControl.ReadAllTasksAndFilter();

            // If the number of tasks found havne't changed and we haven't tried too many times, reset the timer to retry this code
            // The task list count needs to stabilize, then we can stop checking
            if (retrySolutionOpenedCount < 20)
            {
                if (toolWindowControl.TaskListCount == 0 || toolWindowControl.TaskListCount != currentListCount)
                {
                    // Update the local variable
                    currentListCount = toolWindowControl.TaskListCount;
                    // Reset the timer to check again in the timeout time
                    solutionOpenedDelayTimer.Reset();
                    // Increment the count
                    retrySolutionOpenedCount++;
                }
            }
        }
        #endregion
    }
}
