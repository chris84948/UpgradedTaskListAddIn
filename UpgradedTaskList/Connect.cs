using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Resources;
using System.Reflection;
using System.Timers;
using System.Globalization;
using System.Diagnostics;

namespace UpgradedTaskList
{
    /// <summary>The object for implementing an Add-in.</summary>
    /// <seealso class='IDTExtensibility2' />
    public class Connect : IDTExtensibility2, IDTCommandTarget
    {
        private Window toolWindow;
        private TaskListControl toolWindowControl;
        private EnvDTE.SolutionEvents solutionEvents;
        private EnvDTE.TextEditorEvents textEditorEvents;
        private EnvDTE.WindowEvents windowEvents;
        private DTE2 _applicationObject;
        private AddIn _addInInstance;

        /// <summary>
        /// Delay before firing the command to re-read tasks
        /// Stops the tasks being read every time something is typed
        /// Only when no text has changed in the editor for the UPDATE_DELAY duration will the updates occur.
        /// Also, the application's task list doesn't update for at least 1 second after changes are made.
        /// </summary>
        private System.Timers.Timer taskUpdateDelayTimer;
        private const int UPDATE_DELAY = 3000;

        /// <summary>
        /// Delay before reading tasks after a new solution opens
        /// There is an in-built delay in the standard task list before it gets updated
        /// </summary>
        private System.Timers.Timer solutionOpenedDelayTimer;
        private const int SOLUTION_OPENED_DELAY = 3000;
        private int retrySolutionOpenedCount;
        private int currentListCount;

        /// <summary>Implements the constructor for the Add-in object. Place your initialization code within this method.</summary>
        public Connect()
        {
        }

        /// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
        /// <param term='application'>Root object of the host application.</param>
        /// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
        /// <param term='addInInst'>Object representing this Add-in.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            _applicationObject = (DTE2)application;
            _addInInstance = (AddIn)addInInst;

            // Setup all the solution events
            solutionEvents = (EnvDTE.SolutionEvents)_applicationObject.Events.SolutionEvents;
            solutionEvents.BeforeClosing += new _dispSolutionEvents_BeforeClosingEventHandler(solutionEvents_BeforeClosing);
            solutionEvents.Opened += new _dispSolutionEvents_OpenedEventHandler(solutionEvents_Opened);

            // Setup all text editor events
            textEditorEvents = (EnvDTE.TextEditorEvents)_applicationObject.Events.TextEditorEvents;
            textEditorEvents.LineChanged += new _dispTextEditorEvents_LineChangedEventHandler(textEditorEvents_LineChanged);

            // Setup all window events
            windowEvents = (EnvDTE.WindowEvents)_applicationObject.Events.WindowEvents;
            windowEvents.WindowActivated += new _dispWindowEvents_WindowActivatedEventHandler(windowEvents_WindowActivated);

            switch (connectMode)
            {
                case ext_ConnectMode.ext_cm_UISetup:
                    // We should never get here, this is temporary UI
                    break;

                case ext_ConnectMode.ext_cm_Startup:
                    // The add-in was marked to load on startup
                    AddToolWindow();
                    AddToolWindowMenuItem();
                    break;

                case ext_ConnectMode.ext_cm_AfterStartup:
                    // The add-in was loaded by hand after startup using the Add-In Manager
                    // Initialize it in the same way that when is loaded on startup
                    AddToolWindow();
                    AddToolWindowMenuItem();
                    break;
            }
        }

        /// <summary>
        /// Loads the tool window for upgraded task window
        /// </summary>
        private void AddToolWindow()
        {
            // Load tool window region
            object programmableObject = null;
            string guidString = "{9FFC9D9B-1F39-4763-A2AF-66AED06C799E}";
            Windows2 windows2 = (Windows2)_applicationObject.Windows;
            Assembly asm = Assembly.GetExecutingAssembly();
            toolWindow = windows2.CreateToolWindow2(_addInInstance, asm.Location, "UpgradedTaskList.TaskListControl", "Upgraded Task List", guidString, ref programmableObject);
            toolWindow.Visible = true;

            // Instantiate the TaskListControl WPF object
            toolWindowControl = (TaskListControl)toolWindow.Object;
            // Load all task items to the task window
            toolWindowControl.InitializeViewModel(ref _applicationObject);

            taskUpdateDelayTimer = new System.Timers.Timer(UPDATE_DELAY);
            taskUpdateDelayTimer.AutoReset = false;
            taskUpdateDelayTimer.Elapsed += new ElapsedEventHandler(taskUpdateTimer_Elapsed);

            solutionOpenedDelayTimer = new System.Timers.Timer(SOLUTION_OPENED_DELAY);
            solutionOpenedDelayTimer.AutoReset = false;
            solutionOpenedDelayTimer.Elapsed += new ElapsedEventHandler(solutionOpenedTimer_Elapsed);

            SetTabNameOnWindow(guidString);
        }

        /// <summary>
        /// Adds the Upgraded Task Window menu item to the tool window items
        /// </summary>
        private void AddToolWindowMenuItem()
        {
            object[] contextGUIDS = new object[] { };
            Commands2 commands = (Commands2)_applicationObject.Commands;
            string viewMenuName;

            try
            {
                //If you would like to move the command to a different menu, change the word "View" to the 
                //  English version of the menu. This code will take the culture, append on the name of the menu
                //  then add the command to that menu. You can find a list of all the top-level menus in the file
                //  CommandBar.resx.
                ResourceManager resourceManager = new ResourceManager("UpgradedTaskList.TaskListControl", Assembly.GetExecutingAssembly());
                CultureInfo cultureInfo = new System.Globalization.CultureInfo(_applicationObject.LocaleID);
                string resourceName = String.Concat(cultureInfo.TwoLetterISOLanguageName, "View");
                viewMenuName = resourceManager.GetString(resourceName);
            }
            catch
            {
                //We tried to find a localized version of the word View, but one was not found.
                //  Default to the en-US word, which may work for the current culture.
                viewMenuName = "View";
            }

            //Place the command on the view menu.
            //Find the MenuBar command bar, which is the top-level command bar holding all the main menu items:
            Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)_applicationObject.CommandBars)["MenuBar"];

            //Find the View command bar on the MenuBar command bar:
            CommandBarControl viewControl = menuBarCommandBar.Controls[viewMenuName];
            CommandBarPopup viewPopup = (CommandBarPopup)viewControl;

            int taskIndex = 1;
            Boolean commandExists = false;

            //Add a control for the command to the view menu:
            if (viewPopup != null)
            {
                for (int i = 0; i < viewPopup.CommandBar.Controls.Count; i++)
                {
                    try
                    {
                        // Find the location of the standard task list - make sure the upgraded task list is just above it
                        if (viewPopup.CommandBar.Controls[i].Caption.Equals("Tas&k List"))
                        {
                            taskIndex = i;
                        }
                        if (viewPopup.CommandBar.Controls[i].Caption.Equals("Upgraded Task List"))
                        {
                            commandExists = true;
                        }
                    }
                    catch { }
                }
            }

            // If the command doesn't already exist, add it to the menu
            if (!commandExists)
            {
                //Add a command to the Commands collection:
                Command command = commands.AddNamedCommand2(_addInInstance,
                                                            "UpgradedTaskList",
                                                            "Upgraded Task List",
                                                            "Executes the command for ToolWindowArticle",
                                                            true,
                                                            837,
                                                            ref contextGUIDS,
                                                            (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled,
                                                            (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

                command.AddControl(viewPopup.CommandBar, taskIndex);
            }
        }

        /// <summary>
        /// Due to a bug in the way Visual Studio uses WPF add-in windows, this is the only way to change the tab name text
        /// </summary>
        /// <param name="guidString">GUID of the tool window as a string</param>
        private void SetTabNameOnWindow(String guidString)
        {
            try
            {
                // Get the service provider on the object
                Microsoft.VisualStudio.Data.ServiceProvider serviceProvider = new Microsoft.VisualStudio.Data.ServiceProvider(this._applicationObject as Microsoft.VisualStudio.OLE.Interop.IServiceProvider);

                // Get the shell service
                var vsUIShell = (IVsUIShell)serviceProvider.GetService(typeof(SVsUIShell));
                Guid slotGuid = new Guid(guidString);

                // Find the associated window frame on this toolwindow
                IVsWindowFrame wndFrame;
                vsUIShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFrameOnly, ref slotGuid, out wndFrame);

                // Set the text on the window tab name
                wndFrame.SetProperty((int)__VSFPROPID.VSFPROPID_Caption, "Upgraded Task List");
            }
            catch
            {
                // If this fails, no big deal, we're only changing the tab name
            }
        }

        /// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
        /// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
        {
        }

        /// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />		
        public void OnAddInsUpdate(ref Array custom)
        {
        }

        /// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnStartupComplete(ref Array custom)
        {
        }

        /// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnBeginShutdown(ref Array custom)
        {
        }

        /// <summary>Implements the QueryStatus method of the IDTCommandTarget interface. This is called when the command's availability is updated</summary>
        /// <param term='commandName'>The name of the command to determine state for.</param>
        /// <param term='neededText'>Text that is needed for the command.</param>
        /// <param term='status'>The state of the command in the user interface.</param>
        /// <param term='commandText'>Text requested by the neededText parameter.</param>
        /// <seealso class='Exec' />
        public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
        {
            if (neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
            {
                if (commandName == "UpgradedTaskList.Connect.UpgradedTaskList")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
            }
        }

        /// <summary>Implements the Exec method of the IDTCommandTarget interface. This is called when the command is invoked.</summary>
        /// <param term='commandName'>The name of the command to execute.</param>
        /// <param term='executeOption'>Describes how the command should be run.</param>
        /// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
        /// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
        /// <param term='handled'>Informs the caller if the command was handled or not.</param>
        /// <seealso class='Exec' />
        public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
        {
            handled = false;
            if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
            {
                if (commandName == "UpgradedTaskList.Connect.UpgradedTaskList")
                {
                    handled = true;
                    toolWindow.Visible = true;
                    return;
                }
            }
        }

        #region Environment Events

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