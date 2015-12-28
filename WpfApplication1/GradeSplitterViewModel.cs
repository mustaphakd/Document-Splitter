using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
namespace GSPDocumentSpliter
{
    public class GradeSplitterViewModel : ViewModelBase
    {
        GradeSplitter engine = null;
        private string _fileName;
        private string _directory;
        private string _newFileName;
        private System.Windows.Threading.Dispatcher _dispatcher;

        public GradeSplitterViewModel()
        {
            OutputNames = new ObservableCollection<string>();
            GeneratedFiles = new ObservableCollection<string>();
           /* OutputNames.Add("Bonjour");
            OutputNames.Add("dfg");
            OutputNames.Add("we");
            OutputNames.Add("iu");*/

            Run = new RelayCommand(() => {
                var outputFilesname = ComputeFilesFullPath(OutputNames);

                if ((engine != null))
                {
                    if (engine.Running)
                        engine.CancelFileGeneration();

                    engine.SetFilePathAndNames(FileName, outputFilesname);

                }
                else
                {
                    engine = new GradeSplitter(FileName, outputFilesname);
                    engine.FileGenerated += engine_FileGenerated;
                    engine.StatusUpdated += engine_StatusUpdated;
                }

                

                engine.DestinationDirectory = Directory;
                GeneratedFiles.Clear();
                engine.GenerateFiles();

                RaisePropertyChanged(() => Run);
                RaisePropertyChanged(() => Cancel);
            
            }, CanRun);
            Cancel = new RelayCommand(() => {

                if ((engine != null) && (engine.Running))
                    engine.CancelFileGeneration();
            
            }, () => ((engine != null) && (engine.Running)));

            AddName = new RelayCommand(() => {
                if (String.IsNullOrEmpty(_newFileName)) return;
                OutputNames.Add(_newFileName);
                _newFileName = String.Empty;
                RaisePropertyChanged(() => Run);
                RaisePropertyChanged(() => RemoveName);
                RaisePropertyChanged(() => NewFileName);

            }, () => !String.IsNullOrWhiteSpace(_newFileName));

            RemoveName = new RelayCommand(() =>
            {
                if(OnGetSelectedItems != null)
                {
                    var lst = OnGetSelectedItems();
                    if(lst  != null)
                    foreach(var item in lst)
                    {
                        var itm = item.Trim();
                        if (OutputNames.Contains(itm))
                            OutputNames.Remove(itm);
                    }
                }
                else
                    OutputNames.Remove(OutputNames.Last());
                RaisePropertyChanged(() => Run);;
                RaisePropertyChanged(() => RemoveName);
                

            }, () => OutputNames.Count() > 0);

            
        }

        public GradeSplitterViewModel(System.Windows.Threading.Dispatcher dispatcher):this()
        {
            this._dispatcher = dispatcher;
        }

        void engine_StatusUpdated(object sender, Status e)
        {
            switch(e)
            {
                case Status.Completed:
                case Status.Stopped: 
                    Running = false;
                    
                    break;
                case Status.Running:
                    Running = true;
                    break;
                default:
                    break;
            }
            _dispatcher.Invoke(() =>
            {
                RaisePropertyChanged(() => Run);
                RaisePropertyChanged(() => Cancel);
                RaisePropertyChanged(() => Running);

                CommandManager.InvalidateRequerySuggested();
            }
            );

            
        }


        void engine_FileGenerated(object sender, string e)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                GeneratedFiles.Add(e);
            }
            else
            {

                _dispatcher.Invoke(() => GeneratedFiles.Add(e));
            }
        }

        private void ClearHandlers(GradeSplitter engine)
        {
            if(engine != null)
            {
                engine.FileGenerated -= engine_FileGenerated;
                engine.StatusUpdated -= engine_StatusUpdated;
            }
        }

        private IEnumerable<String> ComputeFilesFullPath(IEnumerable<string> OutputNames)
        {
            foreach (var name in OutputNames)
                yield return Path.Combine(Directory, name);
        }
        public String FileName {
            get { return _fileName; }
            set
            {
                if(value != _fileName)
                {
                    _fileName = value;
                    RaisePropertyChanged(() => FileName);
                }

            }
        }

        public String Directory {
            get { return _directory; }
            set
            {
                if(value != _directory)
                {
                    _directory = value;
                    RaisePropertyChanged(() => Directory);
                }
            }
        }

        public bool CanRun()
        {
               return (((!String.IsNullOrWhiteSpace(FileName)) && (!String.IsNullOrWhiteSpace(Directory))
                    && (OutputNames.Count > 0)) && (Running == false)
                    );
           
        }

        public String NewFileName
        {
            get { return _newFileName; }
            set
            {
                if (value != _newFileName)
                {
                    _newFileName = value;
                    RaisePropertyChanged(() => NewFileName);
                    RaisePropertyChanged(() => AddName);
                    RaisePropertyChanged(() => RemoveName);
                }
            }
        }

        public ObservableCollection<string> OutputNames
        {
            get;
            private set;
        }

        public ObservableCollection<string> GeneratedFiles
        {
            get;
            private set;
        }
        

        public ICommand Run { get; private set; }

        public ICommand Cancel { get; private set; }


        public ICommand AddName { get; private set; }

        public ICommand RemoveName { get; private set; }

        public Func<IEnumerable<String>> OnGetSelectedItems { get; set; }

        public Boolean Running { get; private set; }



        internal void LoadFileNames(string filePath)
        {
            OutputNames.Clear();
            Task.Run(() =>
            {
                if(! IsBinaryFile(File.ReadAllBytes(filePath)))
                if (File.Exists(filePath))
                {
                        var content = File.ReadAllLines(filePath);
                        _dispatcher.Invoke(() =>
                        {
                            try
                            {
                                foreach (var line in content)
                                {
                                    if (String.IsNullOrEmpty(line))
                                        continue;

                                    OutputNames.Add(line.Trim());
                                }
                                RaisePropertyChanged(() => Run);
                                CommandManager.InvalidateRequerySuggested();
                            }
                            catch (Exception) { 
                                
                            }
                        });
                }
            });
        }

        private bool IsBinaryFile(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] > 127)
                    return true;
            return false;
        }
    }
}
