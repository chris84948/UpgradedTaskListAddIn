using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.IO;
using System.Reflection;
using System.Linq.Expressions;
using MVVMLibrary;
using EnvDTE;

namespace UpgradedTaskList
{
    class TaskListItemCollectionViewModel : ObservableObject
    {
        #region Variables

        private EnvDTE80.DTE2 applicationObject;
        private String token;
        private int scope;

        private ObservableCollection<TaskListItemViewModel> _taskCollection;
        private ObservableCollection<TaskListItemViewModel> _filteredTaskCollection;
        private String _descriptionSortChar;
        private String _fileSortChar;
        private String _lineSortChar;

        /// <summary>
        /// Collection of all TaskListItemViewModel
        /// </summary>
        public ObservableCollection<TaskListItemViewModel> TaskCollection
        {
            get { return _taskCollection; }
            set 
            {
                _taskCollection = value;
                OnPropertyChanged("TaskCollection");
            }
        }

        /// <summary>
        /// Filtered collection of TaskListItemViewModel for use with the listview in the GUI
        /// </summary>
        public ObservableCollection<TaskListItemViewModel> FilteredTaskCollection
        {
            get { return _filteredTaskCollection; }
            set 
            {
                _filteredTaskCollection = value;
                OnPropertyChanged("FilteredTaskCollection");
            }
        }

        /// <summary>
        /// Description Sorting character - can be "", "▲" or "▼";
        /// </summary>
        public String DescriptionSortChar
        {
            get { return _descriptionSortChar; }
            set
            {
                _descriptionSortChar = value;
                OnPropertyChanged("DescriptionSortChar");
            }
        }

        /// <summary>
        /// File Sorting character - can be "", "▲" or "▼";
        /// </summary>
        public String FileSortChar
        {
            get { return _fileSortChar; }
            set
            {
                _fileSortChar = value;
                OnPropertyChanged("FileSortChar");
            }
        }

        /// <summary>
        /// Line Sorting character - can be "", "▲" or "▼";
        /// </summary>
        public String LineSortChar
        {
            get { return _lineSortChar; }
            set
            {
                _lineSortChar = value;
                OnPropertyChanged("LineSortChar");
            }
        }

        #endregion

        /// <summary>
        /// Constructor, create a TaskListItemViewModel for each TaskItem in TaskList
        /// </summary>
        /// <param name="ApplicationObject">Reference to the main application</param>
        /// <param name="selectedToken">Selected token from the GUI</param>
        /// <param name="selectedScopeIndex">Selected scope index from the GUI</param>
        public TaskListItemCollectionViewModel(ref EnvDTE80.DTE2 ApplicationObject, String selectedToken, int selectedScopeIndex)
        {
            // Store the application object as a local reference
            this.applicationObject = ApplicationObject;

            // Initialize the sort characters
            DescriptionSortChar = SortChar.Asc;
            FileSortChar = SortChar.None;
            LineSortChar = SortChar.None;

            // Initialize the token and scope
            token = "ALL";
            scope = 0;

            GetTasksFromApplicationObject();
            CreateFilteredTaskCollection(selectedToken, selectedScopeIndex);
        }

        public void GetTasksFromApplicationObject()
        {
            // Instantiate the collection first
            TaskCollection = new ObservableCollection<TaskListItemViewModel>();

            // Create the full list of task items
            foreach (TaskItem taskItem in applicationObject.ToolWindows.TaskList.TaskItems)
            {
                try
                {
                    // Only show comment task items
                    if (taskItem.Category.Equals("Comment"))
                    {
                        TaskCollection.Add(new TaskListItemViewModel(taskItem));
                    }
                }
                catch
                {
                    // Don't add this item, do nothing
                }
            }

            // Sort the new task list
            SortTaskCollection();
        }

        /// <summary>
        /// Remove the tasks whose filename matches the currently open file
        /// </summary>
        private void RemoveTasksFromCurrentFile()
        {
            for (int i = TaskCollection.Count - 1; i >= 0; i--)
            {
                if (TaskCollection[i].FullFilename.Equals(GetCurrentFile()))            
                    _taskCollection.RemoveAt(i);
            }
        }

        /// <summary>
        /// Filter the taskitems based on the token string and/or the scope
        /// </summary>
        /// <param name="token">Token to filter on (e.g. TODO or HACK)</param>
        /// <param name="scope">Scope to filter tasks on (e.g. Project or Class)</param>
        public void CreateFilteredTaskCollection(String token, int scope)
        {
            // Copy passed variables into locals (stored for filtering again later)
            this.token = token;
            this.scope = scope;

            FilteredTaskCollection = FilterTasks();            
        }

        /// <summary>
        /// Clears the task collection when changing projects
        /// </summary>
        public void ClearTaskCollections()
        {
            TaskCollection.Clear();
            FilteredTaskCollection.Clear();
        }

        /// <summary>
        /// Filter all tasks - filters based on the locally stored variables (can be re-run internally)
        /// </summary>
        /// <returns>Collection of all task list items</returns>
        private ObservableCollection<TaskListItemViewModel> FilterTasks()
        {
            List<String> projectFiles = new List<String>();
            String currentFile = "";

            // Create a temporary list (can't change observablecollection from different thread it was created on, we call this from a timer)
            ObservableCollection<TaskListItemViewModel> tempFilteredTaskCollection = new ObservableCollection<TaskListItemViewModel>();

            // Before we start looping through the tasks check the scope
            // If it's Project or Class, get a list of filenames of the files that match
            if (scope == (int)TaskListControlViewModel.taskScope.Project)
            {
                projectFiles = GetFilesInProject();
            }
            else if (scope == (int)TaskListControlViewModel.taskScope.Class)
            {
                currentFile = GetCurrentFile();
            }

            // Add the item if it has the matching token
            foreach (TaskListItemViewModel taskItem in TaskCollection)
            {
                // First check the token to look for a match ("ALL" is a special case, with an obvious use)
                if (token.Equals("ALL") || taskItem.Token.Equals(token))
                {
                    // Now check the scope option
                    // Solution will just add the item - it doesn't filter at all
                    if (scope == (int)TaskListControlViewModel.taskScope.Solution)
                    {
                        tempFilteredTaskCollection.Add(taskItem);
                    }
                    // Project needs to check the taskItem file versus files in the project for matches
                    else if (scope == (int)TaskListControlViewModel.taskScope.Project)
                    {
                        // If the list of project files contains the taskitems filename then add it to the filtered list
                        if (projectFiles.Contains(taskItem.FullFilename))
                            tempFilteredTaskCollection.Add(taskItem);
                    }
                    // Class looks for task items matching only the class open in the editor
                    else if (scope == (int)TaskListControlViewModel.taskScope.Class)
                    {
                        if (currentFile.Equals(taskItem.FullFilename))
                            tempFilteredTaskCollection.Add(taskItem);
                    }
                }
            }

            return tempFilteredTaskCollection;
        }

        /// <summary>
        /// Returns the current filename of the file open in the editor
        /// </summary>
        /// <returns>current filename of the file open in the editor</returns>
        public string GetCurrentFile()
        {
            try
            {
                return applicationObject.ActiveDocument.FullName;
            }
            catch
            {
                // If this file doens't exist, return a blank string
                return "";
            }

        }

        /// <summary>
        /// Gets a list of all files contained in the current open project
        /// </summary>
        /// <returns>List of all files in the current project</returns>
        private List<string> GetFilesInProject()
        {
            List<String> files = new List<String>();

            // Get the active project
            Project activeProject = GetActiveProject();

            if (activeProject != null)
            {
                // Now loop through all the project items and add the filenames to the projectfiles list
                foreach (ProjectItem item in activeProject.ProjectItems)
                {
                    if (item.FileNames[0].EndsWith("\\"))
                    {
                        // This is a folder, so add all the items in the subfolder (doesn't really matter if some of them are not really appropriate
                        files.AddRange(Directory.GetFiles(item.FileNames[0], "*", SearchOption.AllDirectories));
                    }
                    else
                    {
                        // For some reason, the filename is a list, with the actual filename at 0 index
                        files.Add(item.FileNames[0]);
                    }
                }
            }
            
            return files;
        }

        /// <summary>
        /// Returns the active project as a project object
        /// </summary>
        /// <returns>Project object</returns>
        internal Project GetActiveProject()
        {
            Project activeProject = null;
 
            Array activeSolutionProjects = applicationObject.ActiveSolutionProjects as Array;
            if (activeSolutionProjects != null && activeSolutionProjects.Length > 0)
            {
                activeProject = activeSolutionProjects.GetValue(0) as Project;
            }
 
            return activeProject;
        }

        /// <summary>
        /// Clears all sort characters on the grid view column headers then set the correct one
        /// </summary>
        /// <param name="sortChar">Line Sorting character - can be "", "▲" or "▼";</param>
        private String UpdateSortChars(String sortChar)
        {
            // First, set all sortChars to blank first
            DescriptionSortChar = SortChar.None;
            LineSortChar = SortChar.None;
            FileSortChar = SortChar.None;

            // Return the sort char of the character to update (the one being sorted)
            return sortChar;
        }
        
        /// <summary>
        /// Sort Tasks based on the current sort character
        /// </summary>
        private void SortTaskCollection()
        {
            // Sort the tasks based on whatever sort is currently setup
            if (DescriptionSortChar.Equals(SortChar.Asc))
            {
                TaskCollection = new ObservableCollection<TaskListItemViewModel>(TaskCollection.OrderBy(o => o.Description).ToList());
            }
            else if (DescriptionSortChar.Equals(SortChar.Desc))
            {
                TaskCollection = new ObservableCollection<TaskListItemViewModel>(TaskCollection.OrderByDescending(o => o.Description).ToList());
            }
            else if (FileSortChar.Equals(SortChar.Asc))
            {
                TaskCollection = new ObservableCollection<TaskListItemViewModel>(TaskCollection.OrderBy(o => o.Filename).ToList());
            }
            else if (FileSortChar.Equals(SortChar.Desc))
            {
                TaskCollection = new ObservableCollection<TaskListItemViewModel>(TaskCollection.OrderByDescending(o => o.Filename).ToList());
            }
            else if (LineSortChar.Equals(SortChar.Asc))
            {
                TaskCollection = new ObservableCollection<TaskListItemViewModel>(TaskCollection.OrderBy(o => o.Line).ToList());
            }
            else if (LineSortChar.Equals(SortChar.Desc))
            {
                TaskCollection = new ObservableCollection<TaskListItemViewModel>(TaskCollection.OrderByDescending(o => o.Line).ToList());
            }

            // Now refilter the items based on the sorted list
            FilteredTaskCollection = FilterTasks();
        }

        #region Commands

        /// <summary>
        /// Command for sorting on description
        /// </summary>
        public ICommand SortOnDescription { get { return new RelayCommand(SortOnDescriptionExecute, CanSortOnDescriptionExecute); } }

        private void SortOnDescriptionExecute()
        {
            // If the current sort character is for none or down, then sort ascending
            if (DescriptionSortChar.Equals(SortChar.None) || DescriptionSortChar.Equals(SortChar.Desc))
            {
                DescriptionSortChar = UpdateSortChars(SortChar.Asc);
            }
                // Otherwise sort descending
            else if (DescriptionSortChar.Equals(SortChar.Asc))
            {
                DescriptionSortChar = UpdateSortChars(SortChar.Desc);
            }

            SortTaskCollection();
        }

        private bool CanSortOnDescriptionExecute()
        {
            return true;
        }

        /// <summary>
        /// Command for sorting on File
        /// </summary>
        public ICommand SortOnFile { get { return new RelayCommand(SortOnFileExecute, CanSortOnFileExecute); } }

        private void SortOnFileExecute()
        {
            // If the current sort character is for none or down, then sort ascending
            if (FileSortChar.Equals(SortChar.None) || FileSortChar.Equals(SortChar.Desc))
            {
                FileSortChar = UpdateSortChars(SortChar.Asc);
            }
            // Otherwise sort descending
            else if (FileSortChar.Equals(SortChar.Asc))
            {
                FileSortChar = UpdateSortChars(SortChar.Desc);
            }

            SortTaskCollection();
        }

        private bool CanSortOnFileExecute()
        {
            return true;
        }

        /// <summary>
        /// Command for sorting on line
        /// </summary>
        public ICommand SortOnLine { get { return new RelayCommand(SortOnLineExecute, CanSortOnLineExecute); } }

        private void SortOnLineExecute()
        {
            // If the current sort character is for none or down, then sort ascending
            if (LineSortChar.Equals(SortChar.None) || LineSortChar.Equals(SortChar.Desc))
            {
                LineSortChar = UpdateSortChars(SortChar.Asc);
            }
            // Otherwise sort descending
            else if (LineSortChar.Equals(SortChar.Asc))
            {
                LineSortChar = UpdateSortChars(SortChar.Desc);
            }

            SortTaskCollection();
        }

        private bool CanSortOnLineExecute()
        {
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Essentially works as a String enum
    /// </summary>
    internal static class SortChar {
        internal static String None = "";
        internal static String Asc = "▲";
        internal static String Desc = "▼";
    }
}
