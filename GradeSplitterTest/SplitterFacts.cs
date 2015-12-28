using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GSPDocumentSpliter;

namespace GradeSplitterTest
{    
    public class SplitterFacts
    {
        private static string _path;
        private static bool _pathInit = false;



        [Fact]
        public void Count_Document_pages() {
            //Arrange
            var documentSplitter = new GradeSplitter(FilePath);

            //Act
            var count = documentSplitter.Count();
                
            //Assert
            Assert.Equal(4, count);        
        }

        [Fact]
        public void Index_of_pages()
        {
            //Arrange
            var documentSplitter = new GradeSplitter(FilePath);

            //Act
            var pageIndexes = documentSplitter.GetPageIndexes();

            //Assert
            Assert.Equal(2, pageIndexes[1].Count);
        }

        public static String FilePath { get {
            if (!_pathInit)
            {
                _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "arts", "testpageBreaks.docx");
                _pathInit = true;
            }
            return _path;
        } }
    }
    
}
