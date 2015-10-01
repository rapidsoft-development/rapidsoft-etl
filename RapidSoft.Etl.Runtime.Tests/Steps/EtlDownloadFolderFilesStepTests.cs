using System;
using System.Configuration;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RapidSoft.Etl.Logging;
using RapidSoft.Etl.Runtime.Steps;

namespace RapidSoft.Etl.Runtime.Tests.Steps
{
    [TestClass]
    public class EtlDownloadFolderFilesStepTests
    {
        #region Fields

        private static readonly string _ftpPath = ConfigurationManager.AppSettings["Files"];

        #endregion

        #region Initialization

        [TestInitialize]
        public void Initialize()
        {
        }

        #endregion

        #region Tests

        [TestMethod]
        [DeploymentItem("Files")]
        public void CanDownloadFilesFromFtp()
        {
            var sourceUrl = _ftpPath;
            var destinationFileName = @"Test";

            var package = new EtlPackage
            {
                Id = Guid.NewGuid().ToString(),
                Steps =
                {
                    new EtlDownloadFolderFilesStep()
                    {
                        Source = new EtlResourceInfo
                        {
                            Uri = sourceUrl,
                            //Method = "RETR",
                        },
                        Destination = new EtlFileInfo
                        {
                            FilePath = destinationFileName
                        }
                    }
                }
            };

            var logger = new MemoryEtlLogger();
            var session = package.Invoke(logger);

            Assert.AreEqual(1, logger.EtlSessions.Count);
            Assert.AreEqual(package.Id, logger.EtlSessions[0].EtlPackageId);
            Assert.AreEqual(session.EtlSessionId, logger.EtlSessions[0].EtlSessionId);
            Assert.AreEqual(EtlStatus.Succeeded, logger.EtlSessions[0].Status);

            Assert.AreEqual(EtlStatus.Succeeded, session.Status);
            Assert.IsTrue(File.Exists(destinationFileName));

            var destinationData = File.ReadAllLines(destinationFileName);
            Assert.IsNotNull(destinationData);
            Assert.AreNotEqual("", destinationData);
        }

        #endregion
    }
}