﻿using Microsoft.ApplicationInsights;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TfsWitMigrator.Core.ComponentContext;

namespace TfsWitMigrator.Core
{
   public class MigrationEngine
    {
        List<ITfsProcessingContext> processors = new List<ITfsProcessingContext>();
        List<Action<WorkItem, WorkItem>> processorActions = new List<Action<WorkItem, WorkItem>>();
        Dictionary<string, List<IFieldMap>> fieldMapps = new Dictionary<string, List<IFieldMap>>();
        Dictionary<string, IWitdMapper> workItemTypeDefinitions = new Dictionary<string, IWitdMapper>();
        ITeamProjectContext source;
        ITeamProjectContext target;
        string reflectedWorkItemIdFieldName = "TfsMigrationTool.ReflectedWorkItemId";

        public Dictionary<string, IWitdMapper> WorkItemTypeDefinitions
        {
            get
            {
                return workItemTypeDefinitions;
            }
        }

        public ITeamProjectContext Source
        {
            get
            {
                return source;
            }
        }

        public ITeamProjectContext Target
        {
            get
            {
                return target;
            }
        }
        public string ReflectedWorkItemIdFieldName
        {
            get { return reflectedWorkItemIdFieldName;}
        }

        public ProcessingStatus Run()
        {
            var measurements = new Dictionary<string, double>();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            TelemetryClient tc = new TelemetryClient();
            tc.Context.User.Id = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            tc.Context.Session.Id = Guid.NewGuid().ToString();
            measurements.Add("Processors", processors.Count);
            measurements.Add("Actions", processorActions.Count);
            measurements.Add("Mappings", fieldMapps.Count);
            tc.TrackEvent("MigrationEngine:Run", null, measurements);
            ProcessingStatus ps = ProcessingStatus.Complete;
            foreach (ITfsProcessingContext process in processors)
            {
                process.Execute();
                if (process.Status == ProcessingStatus.Failed)
                {
                    ps = ProcessingStatus.Failed;
                    Trace.WriteLine("The Processor {0} entered the failed state...stopping run", process.Name);
                    break;
                }
            }
            stopwatch.Stop();
            tc.TrackMetric("RunTime", stopwatch.ElapsedMilliseconds);
            return ps;
        }

        public void AddProcessor<TProcessor>()
        {
            ITfsProcessingContext pc = (ITfsProcessingContext)Activator.CreateInstance(typeof(TProcessor), new object[] { this });
            AddProcessor(pc);
        }

        public void AddProcessor(ITfsProcessingContext processor)
        {
            processors.Add(processor);
        }


        public void SetReflectedWorkItemIdFieldName(string fieldName)
        {
            reflectedWorkItemIdFieldName = fieldName;
        }

        public void SetSource(ITeamProjectContext teamProjectContext)
        {
            source = teamProjectContext;
        }

        public void SetTarget(ITeamProjectContext teamProjectContext)
        {
            target = teamProjectContext;
        }

        public void AddFieldMap(string workItemTypeName, IFieldMap fieldToTagFieldMap)
        {
            if (!fieldMapps.ContainsKey(workItemTypeName))
            {
                fieldMapps.Add(workItemTypeName, new List<IFieldMap>());
            }
            fieldMapps[workItemTypeName].Add(fieldToTagFieldMap);
        }
         public void AddWorkItemTypeDefinition(string workItemTypeName, IWitdMapper workItemTypeDefinitionMap = null)
        {
            if (!workItemTypeDefinitions.ContainsKey(workItemTypeName))
            {
                workItemTypeDefinitions.Add(workItemTypeName, workItemTypeDefinitionMap);
            }
        }

        internal void ApplyFieldMappings(WorkItem source, WorkItem target)
        { 
            if (fieldMapps.ContainsKey("*"))
            {
                ProcessFieldMapList(source, target, fieldMapps["*"]);
            }
            if (fieldMapps.ContainsKey(source.Type.Name))
            {
                ProcessFieldMapList(source, target, fieldMapps[source.Type.Name]);
            }
        }

        internal void ApplyFieldMappings(WorkItem target)
        {
            if (fieldMapps.ContainsKey("*"))
            {
                ProcessFieldMapList(target, target, fieldMapps["*"]);
            }
            if (fieldMapps.ContainsKey(target.Type.Name))
            {
                ProcessFieldMapList(target, target, fieldMapps[target.Type.Name]);
            }
        }

        private  void ProcessFieldMapList(WorkItem source, WorkItem target, List<IFieldMap> list)
        {
            foreach (IFieldMap map in list)
            {
                map.Execute(source, target);
            }
        }


    }
}
