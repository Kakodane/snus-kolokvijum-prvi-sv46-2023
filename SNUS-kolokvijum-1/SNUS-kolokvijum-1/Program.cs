using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNUS_kolokvijum_1
{
    internal class Program
    {
        private readonly static object _lock = new object();
        static async Task Main(string[] args)
        {
            string xml = File.ReadAllText("SystemConfig.xml");

            XElement root = XElement.Parse(xml);

            int workerCount = int.Parse(root.Element("WorkerCount")?.Value);
            int maxQueueSize = int.Parse(root.Element("MaxQueueSize")?.Value);
            ProcessingSystem system = new ProcessingSystem(workerCount, maxQueueSize);
            List<Job> jobs = root.Element("Jobs")
                .Elements("Job")
                .Select(x => new Job
                {
                    Type = Enum.Parse<JobType>(x.Attribute("Type")?.Value),
                    Payload = x.Attribute("Payload")?.Value,
                    Priority = int.Parse(x.Attribute("Priority")?.Value)
                })
                .ToList();
            List<Task> workers = new List<Task>();
            List<JobHandle> jobHandles= new List<JobHandle>();
            object listLock = new object();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(125));
            for (int i = 0; i < workerCount; i++)
            {
                workers.Add(Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        int index = Random.Shared.Next(jobs.Count);
                        Job randomJob = jobs[index];
                        
                        if (randomJob != null)
                        {
                            Job job = new Job();
                            job.Type = randomJob.Type;
                            job.Payload = randomJob.Payload;
                            job.Priority= randomJob.Priority;
                            JobHandle jh = system.Submit(job);
                            if (jh != null)
                            {
                                lock (listLock)
                                {
                                    jobHandles.Add(jh);
                                }
                            }
                            await Task.Delay(Random.Shared.Next(100, 1000));
                        }
                    }
                }));
            }
            await Task.WhenAll(workers);
            Console.WriteLine("Waiting for workers...");
            var allTasks = jobHandles.Select(jh => jh.Result);

            try
            {
                await Task.WhenAll(allTasks);
                Console.WriteLine("All jobs are processed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            await Task.WhenAll(ProcessingSystem.writeTasks);
             Console.WriteLine("All jobs are written in file!");
        }
    }

    
}
