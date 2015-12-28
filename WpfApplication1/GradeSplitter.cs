using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenXmlPowerTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WoroArch.Infrastructure.Desktop;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace GSPDocumentSpliter
{

    //public delegate void FileGenerationCompleted(Object sender, Event)
    public class GradeSplitter
    {
        private string FilePath;
        private IEnumerable<string> _fileNames;
        private CancellationTokenSource _cancelationToken;
        private bool _configured;
        private ServiceRequestsScheduler scheduler;
        private static Object _synch = new object();        


        /// <summary>
        /// 
        /// </summary>
        /// <param name="FilePath">absolute path to the source file to be split</param>
        /// <param name="generatedFilesNames"> the names of the files to be generated should be ordered to match their position in the source file</param>

        public GradeSplitter(string FilePath, IEnumerable<string> generatedFilesNames = null)
        {
            this.FilePath = FilePath;
            _fileNames = generatedFilesNames;
           // CreatedFiles = new List<String>();
        }

        /// <summary>
        /// Can be set if engine is not running
        /// </summary>
        public void SetFilePathAndNames(string path , IEnumerable<string>  fileNames )
        {
            if (Running) return;
            FilePath = path;
            _fileNames = fileNames;
        }

        /// <summary>
        /// counts the number of pages in a <see cref="WordprocessingDocument"/>
        /// </summary>
        /// <returns>number of pages identified</returns>
        public Int32 Count()
        {
            var count = 1;
            var breakDetected = false;

            using(var document = WordprocessingDocument.Open(FilePath, false))
            {
                var body = document.MainDocumentPart.Document.Body;
                var blocks = (from ele in body.ChildElements
                            where !(ele is SectionProperties)
                            let pr = ele as Paragraph
                            let containBreak = pr.FirstChild is Run && pr.FirstChild.GetFirstChild<Break>() != null
                            select new
                            {
                                Element = pr,
                                ContainBreak = containBreak
                            }).ToList();

                foreach (var block in blocks)
                {
                    if(breakDetected)
                    {
                        count++;
                        breakDetected = false;
                    }
                    if (block.ContainBreak)
                    {
                        breakDetected = true;
                        continue;
                    }
                }
            }

            

            return count;
        }

        public String DestinationDirectory { get; set; }
        internal PageIndex[]  GetPageIndexes()
        {
            var index = 0;
            var createNew = true;
            var breakDetected = false;
            var lst = new List<PageIndex>();
            using (var document = WordprocessingDocument.Open(FilePath, false))
            {
                var body = document.MainDocumentPart.Document.Body;
                var blocks = (from ele in body.ChildElements
                              where !(ele is SectionProperties)
                              let pr = ele as Paragraph
                              let tbl = ele as DocumentFormat.OpenXml.Wordprocessing.Table
                              let runChild = LocateRunChildContainingBreak(pr, tbl)
                              let containBreak =  (runChild != null) && runChild.Elements<Break>() != null
                              let childrenCount = (pr != null ) ? pr.ChildElements.Count() : tbl.ChildElements.Count()
                              select new
                              {
                                  Element = pr != null ? (OpenXmlCompositeElement)pr : tbl,
                                  ContainBreak = containBreak,
                                  ChildrenCount = childrenCount
                              }).ToList();

                foreach (var block in blocks)
                {
                    var currentIdx = index++;
                    if (breakDetected)
                    {
                        breakDetected = false;
                        createNew = true;
                    }
                    if (block.ContainBreak)
                    {
                        breakDetected = true;
                        if (block.ChildrenCount <= 1) // this is a paragraph that contains only a run with a break
                            continue;
                        else
                            createNew = true;

                        
                    }

                    if (createNew)
                    {
                        var pgIdx = new PageIndex { Index = currentIdx, Count = 1 };
                        lst.Add(pgIdx);
                        createNew = false;

                        if (breakDetected)
                            pgIdx.ContainBreak = true;

                        if ((block.Element is DocumentFormat.OpenXml.Wordprocessing.Table) && (block.ChildrenCount > 1)) // break detected is within a populous table
                            breakDetected = false;

                        if (block.Element is DocumentFormat.OpenXml.Wordprocessing.Table)
                            pgIdx.BlockElementType = BlockElementType.Table;
                        else if (block.Element is DocumentFormat.OpenXml.Wordprocessing.Paragraph)
                            pgIdx.BlockElementType = BlockElementType.Paragraph;
                    }
                    else
                    {
                        var last = lst.LastOrDefault();
                        if (last != null)
                            last.Count += 1;

                        if (breakDetected)
                            last.ContainBreak = true;
                    }
                }
            }
            
            return lst.ToArray();
        }

        [MethodImpl(  MethodImplOptions.Synchronized)]
        private static Run LocateRunChildContainingBreak(Paragraph pr, DocumentFormat.OpenXml.Wordprocessing.Table tbl)
        {
            if(pr != null)
            {
                var rns = pr.Elements<Run>();
                var found = rns.Where(r => r.Descendants<Break>().Count() > 0).FirstOrDefault();

                return found;

            }
            else if( tbl != null)
            {
                var runs = tbl.Descendants<Run>().Where(rn => rn.Elements<Break>() != null && rn.Elements<Break>().Count() > 0); //.FirstOrDefault();

                if (runs != null && runs.Count() > 0)
                {
                    var firsRun = runs.ElementAt(0);

                    var breaks = runs.ElementAt(0).Elements<Break>();

                    if (breaks != null && breaks.Count() > 0)
                    {
                        var firstBreawk = breaks.ElementAt(0);

                        if (breaks != null)
                            return firsRun;
                    }
                }

            }
            return null;  //(pr != null) ? (pr.Elements<Run>() != null && pr.Elements<Run>().Count() > 0) ? pr.Elements<Run>().ElementAt(0) as Run : null : null;
        }

        public  void GenerateFiles()
        {
            if (!_configured)
                ConfigureScheduler();
            _cancelationToken = new CancellationTokenSource();
            Counter = _fileNames.Count();
            var pages = GetPageIndexes();

            pages =  RemoveEmptyGraphs(pages);

            var counter = 0;
            UpdateToRunningState();


            if(CreatedFiles != null)
                CreatedFiles.Clear();
            
            foreach(var fileName in _fileNames)
            {
                var wmlDoc = new WmlDocument(FilePath);
                if(_cancelationToken.IsCancellationRequested)
                    break;
                
                var currentPage = pages[counter++];
                scheduler.EnqueueRequest(() => {
                    var fileWtExtension = string.Format("{0}.docx", fileName);
                    var destination = Path.Combine(DestinationDirectory, fileWtExtension);
                    //var sources = new List<Source>(){new Source(wmlDoc, currentPage.Index, currentPage.Count, true){ DiscardHeadersAndFootersInKeptSections = false, KeepSections = true},}; // true to keep sections : header and footer
                   //  ** var sources = new List<Source>() { new Source(wmlDoc, true) { DiscardHeadersAndFootersInKeptSections = false, KeepSections = true }, }; // true to keep sections : header and footer
                    //DocumentBuilder.BuildDocument(sources, );

                   // WmlDocument wmlDc = DocumentBuilder.BuildDocument(sources);

                    WmlDocument wmlDc = BuildDocument(wmlDoc, currentPage);

                    if(currentPage.ContainBreak && currentPage.BlockElementType == BlockElementType.Paragraph)
                    {
                        RemovePageBreak(wmlDc);                        
                    }

                    RemovePlainParagraphs(wmlDc);
                    
                    //ApplyHeader(wmlDoc, wmlDc);

                    wmlDc.SaveAs(destination);

                    FileCompletionNotifier(fileWtExtension);

                }, _cancelationToken.Token);
            }
            
        }

        private void RemovePlainParagraphs(WmlDocument wmlDc)
        {
            var updatesMade = false;
            using(var mem = new MemoryStream())
            {
                mem.Write(wmlDc.DocumentByteArray, 0, wmlDc.DocumentByteArray.Length);

                using(var wordDoc = WordprocessingDocument.Open(mem, true))
                {
                    var rootBlocks = wordDoc.MainDocumentPart.Document.Body.Elements().Where(el => !(el is SectionProperties) );
                    var length = rootBlocks.Count();

                    for (var i = length - 1; i >= 0; i-- )
                    {
                        var prgrph = rootBlocks.ElementAt(i) as Paragraph;

                        if(prgrph != null)
                        {
                            var pr_children = prgrph.Elements().Where(el => !( el is ParagraphProperties));

                            if(pr_children == null  || pr_children.Count() < 1)
                            {
                                prgrph.Remove();
                                updatesMade = true;
                            }
                        }
                    }

                        wordDoc.MainDocumentPart.Document.Save();
                }
                if (updatesMade)
                    wmlDc.DocumentByteArray = mem.ToArray();
            }
        }

        private PageIndex[] RemoveEmptyGraphs(PageIndex[] pages)
        {
            var pgIdxLst = new List<PageIndex>(pages);
            var length = pages.Length;

            using (var document = WordprocessingDocument.Open(FilePath, false))
            {
                var body = document.MainDocumentPart.Document.Body;
                var blckElements = body.ChildElements;
                for (var i = length - 1; i >= 0; i--)
                {
                    var currentPage = pages[i];

                    if(currentPage.BlockElementType == BlockElementType.Paragraph)
                    {
                        var paragraphElement = blckElements[currentPage.Index];

                        var runs = paragraphElement.Elements<Run>();

                        if(runs == null || (runs.Count() < 2))
                        {
                            if(runs.Count() == 1)
                            {
                                var rn = runs.ElementAt(0);

                                if(rn != null)
                                {
                                    var txts = rn.Elements<Text>();
                                    if (txts != null && txts.Count() > 0)
                                        continue;
                                }
                            }
                            pgIdxLst.RemoveAt(i);
                        }
                    }
                }
            }
            return pgIdxLst.ToArray();
        }

        [MethodImpl(  MethodImplOptions.NoOptimization)]
        private WmlDocument BuildDocument(WmlDocument srcDoc, PageIndex pgIndx)
        {
            WmlDocument doc = new WmlDocument(srcDoc, true);

            using(var mem = new MemoryStream())
            {
                mem.Write(doc.DocumentByteArray, 0, doc.DocumentByteArray.Length);

                using(var wordDoc = WordprocessingDocument.Open(mem, true))
                {
                    var elements = wordDoc.MainDocumentPart.Document.Body.Elements().Where(el => !(el is SectionProperties));

                    var length = elements.Count();

                    var maxIdx = (pgIndx.Index + pgIndx.Count) - 1;

                    for (var i = length - 1; i >= 0; i-- )
                    {
                        if(i > maxIdx || i < pgIndx.Index)
                        {
                            elements.ElementAt(i).Remove();
                        }
                    }


                        wordDoc.MainDocumentPart.Document.Save();
                }
                doc.DocumentByteArray = mem.ToArray();
            }

            return doc;
        }

        /// <summary>
        /// apply header from sourceWmlDoc to destWmlDoc; both being of type <see cref="WmlDocument"/>
        /// </summary>
        /// <param name="sourceWmlDoc">Source WmlDocument from which to pull the header</param>
        /// <param name="destWmlDoc"> destination WmlDocument's header being updated</param>
        private static void ApplyHeader(WmlDocument sourceWmlDoc, WmlDocument destWmlDoc)
        {
            using (var sourceStream = new MemoryStream())
            {
                sourceStream.Write(sourceWmlDoc.DocumentByteArray, 0, sourceWmlDoc.DocumentByteArray.Length);
                using (var sourceDoc = WordprocessingDocument.Open(sourceStream, false))
                {
                    var sourceHeader = sourceDoc.MainDocumentPart.HeaderParts.FirstOrDefault();
                    if (sourceHeader != null)
                    {
                        using (var destStream = new MemoryStream())
                        {
                            destStream.Write(destWmlDoc.DocumentByteArray, 0, destWmlDoc.DocumentByteArray.Length);
                            using (var destDoc = WordprocessingDocument.Open(destStream, true))
                            {
                                var destMainPart = destDoc.MainDocumentPart;
                                destMainPart.DeleteParts(destMainPart.HeaderParts);

                                var newHeader = destMainPart.AddNewPart<HeaderPart>();
                                var newHeaderId = destMainPart.GetIdOfPart(newHeader);

                                newHeader.FeedData(sourceHeader.GetStream());

                                var secPrs = destMainPart.Document.Body.Elements<SectionProperties>();
                                if (secPrs == null || secPrs.Count() < 1)
                                {
                                    if (secPrs != null)
                                    {
                                        destMainPart.Document.Body.RemoveAllChildren<SectionProperties>();
                                    }
                                    secPrs = new List<SectionProperties>();
                                    ((List<SectionProperties>)secPrs).Add(new SectionProperties());

                                    destMainPart.Document.Body.Append(secPrs);

                                }
                                foreach (var secPr in secPrs)
                                {
                                    secPr.RemoveAllChildren<HeaderReference>();
                                    secPr.PrependChild<HeaderReference>(new HeaderReference() { Id = newHeaderId });
                                }
                                destMainPart.Document.Save();
                            }
                            //destStream.Flush();
                            destWmlDoc.DocumentByteArray = destStream.ToArray();
                        }
                    }
                }
            }
        }

        [MethodImpl( MethodImplOptions.NoOptimization)]
        private static void RemovePageBreak(WmlDocument wmlDc)
        {
            using (var memStream = new MemoryStream())
            {
                memStream.Write(wmlDc.DocumentByteArray, 0, wmlDc.DocumentByteArray.Length);
                XElement rooBreak = null;
                using (var wpDoc = WordprocessingDocument.Open(memStream, true))
                {
                    var sDoc = wpDoc.MainDocumentPart.GetXDocument();

                    var bdy = sDoc.Root.Elements(W.body).ElementAt(0);
                    var root = bdy.Elements(W.p).FirstOrDefault();
                    var firstRunNode = root.Elements().ElementAt(0);
                    if (root != null &&  firstRunNode != null && firstRunNode.Name == W.r)
                    {
                        rooBreak = firstRunNode.Descendants(W.br).FirstOrDefault();
                        if (rooBreak != null)
                        {
                            firstRunNode.Remove();
                            wpDoc.MainDocumentPart.PutXDocument();

                        }
                    }
                }
                if (rooBreak != null)
                    wmlDc.DocumentByteArray = memStream.ToArray();
            }
        }

        private void UpdateToRunningState()
        {
            Running = true;

            UpdateStatus(Status.Running);
        }

        private void UpdateStatus(Status status)
        {
            var evtHandlrs = StatusUpdated;

            if (evtHandlrs != null)
            {
                evtHandlrs(this, status);
            }
        }

        private void ConfigureScheduler()
        {
            scheduler = new ServiceRequestsScheduler();
            _configured = true;
        }

        private void FileCompletionNotifier(string fileName)
        {
            lock (_synch)
            {
                if (Counter > 0)
                    Counter--;
                else
                    throw new InvalidOperationException("More pages than file name specied!");


                OnFileGenerated(fileName);
                
                if (Counter < 1)
                    OnFileGenerationCompleted();

                
                
            }
        }
        
        public event EventHandler<String> FileGenerated;
        public event EventHandler<Status> StatusUpdated;

        private void OnFileGenerationCompleted()
        {
            Running = false;
            UpdateStatus(Status.Completed);
        }

        private void OnFileGenerated(string fileName)
        {
            if (CreatedFiles != null)
                CreatedFiles.Add(fileName);

            var evt = FileGenerated;

            if(evt != null)
            {
                evt(this, fileName);
            }
        }

        public IList<String> CreatedFiles { get; set; }

        public void CancelFileGeneration()
        {
            if (!Running)
                return;

            //if (_cancelationToken.IsCancellationRequested)
                _cancelationToken.Cancel();

            Running = false;

            UpdateStatus(Status.Stopped);
        }

        public bool Running { get; private set; }

        [DebuggerDisplay("Index: {Index}, BlockType: {BlockElementType}, Count: {Count}, ContainBreak: {ContainBreak}")]
        public class PageIndex
        {
            public Int32 Index { get; set; }
            public Int32 Count { get; set; }

            public BlockElementType BlockElementType { get; set; }

            public bool ContainBreak { get; set; }
        }

        internal int Counter { get; set; }
    }

    public enum Status
    {
        Stopped,
        Running,
        Completed
    }

    public enum BlockElementType
    {
        Table,
        Paragraph
    }
    
}
