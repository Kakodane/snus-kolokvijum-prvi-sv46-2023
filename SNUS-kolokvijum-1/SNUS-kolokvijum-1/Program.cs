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
        private static List<int> doneJobsIds= new List<int>();
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
            List<JobHandle> jobHandles = new List<JobHandle>();
            foreach (Job job in jobs)
            {
                JobHandle jh= await system.Submit(job);
                if (jh == null)
                {
                    Console.WriteLine($"Job with Id: {job.Id} has already been processed. Skipping.");
                    continue;
                }
                Console.WriteLine($"Submitted Job: Type: {job.Type}, Payload: {job.Payload}, Priority: {job.Priority}, JobHandle Id: {jh.Id}");
                    jobHandles.Add(jh);
            }
            await Task.WhenAll(jobHandles.Select(jh => jh.Result));
            //jobs=jobs.OrderBy(x=>x.Priority).ToList();
            //Queue<Job> jobQueue = new Queue<Job>();
            //for(int i=0;i<maxQueueSize && i<jobs.Count; i++)
            //{
            //    jobQueue.Enqueue(jobs[i]);
            //}
            //for(int i=0;i<workerCount && jobQueue.Count>0; i++)
            //{
            //    Job job = jobQueue.Dequeue();
            //    if(doneJobsIds.Contains(job.Id.GetHashCode()))
            //    {
            //        Console.WriteLine($"Job with Id: {job.Id} has already been processed. Skipping.");
            //        continue;
            //    }
            //    JobHandle jobHandle = await ProcessingSystem.Submit(job);
            //    doneJobsIds.Add(jobHandle.Id.GetHashCode());
            //    Console.WriteLine($"Submitted Job: Type: {job.Type}, Payload: {job.Payload}, Priority: {job.Priority}, JobHandle Id: {jobHandle.Id}");
            //}
        }
    }
}
