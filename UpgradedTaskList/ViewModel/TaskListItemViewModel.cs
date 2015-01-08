using System;
using System.Text.RegularExpressions;
using MVVMLibrary;

namespace UpgradedTaskList
{
    class TaskListItemViewModel : ObservableObject
    {
        private TaskListItem _taskListItem;

        /// <summary>
        /// TaskListItem object to offer up to the View
        /// </summary>
        public TaskListItem TaskListItem
        {
            get { return _taskListItem; }
            set { _taskListItem = value; }
        }

        /// <summary>
        /// Token for the task item e.g. TODO
        /// </summary>
        public String Token
        {
            get { return TaskListItem.Token; }
            set
            {
                TaskListItem.Token = value;
                OnPropertyChanged("Token");
            }
        }

        /// <summary>
        /// Description of the task
        /// </summary>
        public String Description
        {
            get { return TaskListItem.Description; }
            set
            {
                TaskListItem.Description = value;
                OnPropertyChanged("Description");
            }
        }

        /// <summary>
        /// Line to locate the task
        /// </summary>
        public int Line
        {
            get { return TaskListItem.Line; }
            set
            {
                TaskListItem.Line = value;
                OnPropertyChanged("Line");
            }
        }

        /// <summary>
        /// Filename containing the task
        /// </summary>
        public String Filename
        {
            get { return TaskListItem.Filename; }
            set
            {
                TaskListItem.Filename = value;
                OnPropertyChanged("Filename");
            }
        }

        /// <summary>
        /// Full filename path including directory
        /// </summary>
        public String FullFilename
        {
            get { return TaskListItem.FullFilename; }
            set { TaskListItem.FullFilename = value; }
        }

        /// <summary>
        /// Actual pointer to the navigate method
        /// </summary>
        public TaskListItem.NavigateToItemDelegate NavigateToItem
        {
            get { return TaskListItem.NavigateToItem; }
            set { TaskListItem.NavigateToItem = value; }
        }

        /// <summary>
        /// Constructor - create the TaskListItem from the supplied TaskItem
        /// </summary>
        /// <param name="taskItem">TaskItem reference from the visual studio environment</param>
        public TaskListItemViewModel(EnvDTE.TaskItem taskItem)
        {
            // Instantiate the object first
            TaskListItem = new TaskListItem();
            
            // The description comes in like this -
            // TODO: Something to fix or else
            // Split the TODO section for the token
            Token = GetTokenFromDescription(taskItem.Description);

            // Take the full description for the description field
            Description = taskItem.Description;

            // Just copy the line straight over
            Line = taskItem.Line;

            // Only take the last file (not including directoty) for the filename
            Filename = GetFilenameFromFullFilename(taskItem.FileName);

            // Full filename is just the filename from the taskItem
            FullFilename = taskItem.FileName;

            // Set the delegate to the navigate method - used later for double clicking the item
            NavigateToItem = taskItem.Navigate;
        }

        /// <summary>
        /// Used to compare the two tasklist items
        /// </summary>
        /// <param name="item">TaskListItem Object</param>
        /// <returns>True if the items are completely equals (all properties)</returns>
        public bool Equals(TaskListItemViewModel item)
        {
            return this.FullFilename.Equals(item.FullFilename) &&
                   this.Token.Equals(item.Token) &&
                   this.Description.Equals(item.Description) &&
                   this.Line == item.Line;
        }

        /// <summary>
        /// Used to compare the two tasklist items
        /// </summary>
        /// <param name="item">TaskListItem Object</param>
        /// <returns>True if the items match (full filename and line number)</returns>
        public bool Matches(TaskListItemViewModel item)
        {
            // We're only comparing the full filename and the line number
            // This will give us enough information to know it's the same task object
            return this.FullFilename.Equals(item.FullFilename) && this.Line == item.Line;
        }

        /// <summary>
        /// Parses out the Token from the main description
        /// </summary>
        /// <param name="description">Description - example "TODO: something to fix!"</param>
        /// <returns>Token without description i.e. TODO</returns>
        private String GetTokenFromDescription(String description)
        {
            // Create a regex to match the first word of the description (alphanumberic and _ characters only)
            Regex regex = new Regex(@"([A-z])\w+");

            return regex.Match(description).Value;
        }

        /// <summary>
        /// Parse the full filename including folders into only the file
        /// </summary>
        /// <param name="fullFilename">Full filename - C:\Users\Chris\Microsoft\Patcher\View\TaskItem.cs</param>
        /// <returns>Only the file - TaskItem.cs</returns>
        private String GetFilenameFromFullFilename(String fullFilename)
        {
            String[] dirs = fullFilename.Split(new Char[] {'\\'});
            return dirs[dirs.Length - 1];
        }
    }
}
