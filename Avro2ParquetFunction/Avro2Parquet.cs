using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Hadoop.Avro;
using Microsoft.Hadoop.Avro.Container;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Parquet;
using Parquet.Data;
using System.Configuration;
using System.IO;

namespace Avro2ParquetFunction
{
    public static class Avro2Parquet
    {


        const string Schema = @"{
            ""type"":""record"",
            ""name"":""EventData"",
            ""namespace"":""Microsoft.ServiceBus.Messaging"",
            ""fields"":[
                         {""name"":""SequenceNumber"",""type"":""long""},
                         {""name"":""Offset"",""type"":""string""},
                         {""name"":""EnqueuedTimeUtc"",""type"":""string""},
                         {""name"":""SystemProperties"",""type"":{""type"":""map"",""values"":[""long"",""double"",""string"",""bytes""]}},
                         {""name"":""Properties"",""type"":{""type"":""map"",""values"":[""long"",""double"",""string"",""bytes""]}},
                         {""name"":""Body"",""type"":[""null"",""bytes""]}
                     ]
        }";

        static CloudStorageAccount storageAccount;

        static string storageAcct = ConfigurationManager.AppSettings["StorageAccount"];
        static string parquetContainerName = ConfigurationManager.AppSettings["ParquetContainerName"];
        static string blobName = ConfigurationManager.AppSettings["BlobName"];


        [FunctionName("Avro2Parquet")]
        public static void Run([BlobTrigger("telemetry-archive/willowtelemetry01/{name}", Connection = "StorageAccount")]Stream myBlob, string name, TraceWriter log)
        {
            DataSet ds = new DataSet(
                new SchemaElement<int>("ObjectID"),
                new SchemaElement<string>("Value"),
                new SchemaElement<string>("ClientID"),
                new SchemaElement<string>("TimeStamp")
            );

            if (myBlob.Length == 508) { return; } // empty dataset

            //log.Info($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");


            var serializer = AvroSerializer.CreateGeneric(Schema);
            var jsonSerializer = new JsonSerializer();


            using (var reader = AvroContainer.CreateGenericReader(myBlob))
            {

                using (var streamReader = new SequentialReader<object>(reader))
                {
                    var results = streamReader.Objects;

                    foreach (var item in results)
                    {
                        var body = ((AvroRecord)item)[5];  // ["Body"] is the 5th element

                        var json = System.Text.Encoding.Default.GetString((byte[])body);
                        var array = (JArray)JsonConvert.DeserializeObject(json);

                        var telemetry = jsonSerializer.Deserialize<Telemetry>(array[0].CreateReader());

                        ds.Add(telemetry.ObjectID, telemetry.Value, telemetry.ClientID, telemetry.TimeStamp.ToString());
                    }

                }
            }

            //log.Info($"{ds.RowCount} Records processed");


            if (ds.RowCount == 0) { return; }

            storageAccount = CloudStorageAccount.Parse(storageAcct);

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(parquetContainerName);

            blobContainer.CreateIfNotExists();

            name = name + ".parquet";

            CloudBlob blob = blobContainer.GetBlobReference(blobName);
            CloudAppendBlob appendBlob = blobContainer.GetAppendBlobReference(name);

            if (appendBlob.Exists())
            {
                appendBlob.Delete(DeleteSnapshotsOption.IncludeSnapshots);
            }

            log.Info($"===> {ds.RowCount} Records, {name}");


            using (var ms = new MemoryStream())
            {
                using (var writer = new ParquetWriter(ms))
                {
                    writer.Write(ds);
                }

                ms.Seek(0, SeekOrigin.Begin);

                appendBlob.UploadFromStream(ms);
            }
        }
    }
}
