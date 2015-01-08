using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using stdole;
using EnvDTE;

namespace UpgradedTaskList
{
    /// <summary>
    /// Interaction logic for TaskList.xaml
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public partial class TaskListControl : UserControl, IDispatch
    {
        private EnvDTE80.DTE2 applicationObject;

        private TaskListControlViewModel viewModel;

        /// <summary>
        /// Reference to the application
        /// </summary>
        public EnvDTE80.DTE2 ApplicationObject
        {
            get { return applicationObject; }
            set { applicationObject = value; }
        }

        /// <summary>
        /// Gets the task list size
        /// </summary>
        public int TaskListCount
        {
            get { return viewModel.TaskViewModel.TaskCollection.Count; }
        }

        /// <summary>
        /// Constructor for TaskList control
        /// </summary>
        public TaskListControl()
        {
            InitializeComponent();            
        }

        /// <summary>
        /// Instantiates the View Model
        /// </summary>
        public void InitializeViewModel(ref EnvDTE80.DTE2 appObject)
        {
            viewModel = new TaskListControlViewModel(ref appObject);
            this.DataContext = viewModel;
        }
        
        /// <summary>
        /// Reads all tasks from the application objet and filters them based on the current token and scope
        /// </summary>
        public void ReadAllTasksAndFilter()
        {
            viewModel.TaskViewModel.GetTasksFromApplicationObject();
            viewModel.TaskViewModel.CreateFilteredTaskCollection(viewModel.SelectedToken, viewModel.SelectedScopeIndex);
        }

        #region Events
        
        /// <summary>
        /// Called when the user double clicks on an item in the tasks list - it navigates to the highlight task item
        /// </summary>
        private void listviewTasks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // If a double click is registered in blank space, do nothing
            if (listviewTasks.SelectedIndex == -1) return;

            try
            {
                // Call navigate to to jump to code
                viewModel.TaskViewModel.FilteredTaskCollection[listviewTasks.SelectedIndex].NavigateToItem();
            }
            catch
            {
                // Do nothing here - if the navigate fails, don't cause an exception
            }
        }

        /// <summary>
        /// Item changed in the scope of the project - will refilter the tasks based on the new filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the filter based on the new values
            viewModel.RefilterTaskList();
        }

        /// <summary>
        /// Selected token selection changed event - will refilter the tasks based on the new filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboToken_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the filter based on the new values
            viewModel.RefilterTaskList();
        }

        /// <summary>
        /// Called when a solution closes - clear the task list
        /// </summary>
        public void SolutionClosing_EventHandler()
        {
            // Clear the task list
            viewModel.TaskViewModel.ClearTaskCollections();
        }

        /// <summary>
        /// Called when a solution opens - Reread the task list
        /// </summary>
        public void SolutionOpened_EventHandler()
        {
            // Read the tasks from the new solution
            viewModel.TaskViewModel.GetTasksFromApplicationObject();
        }

        /// <summary>
        /// Called whenever a new file is opened in the editor
        /// </summary>
        public void WindowActivated_EventHandler()
        {
            // Forces the list of tasks to refilter on the new file information
            viewModel.RefilterTaskListOnFileChange();
        }

        #endregion
    }
}
