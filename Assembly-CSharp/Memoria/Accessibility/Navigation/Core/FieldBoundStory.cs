using System;
using System.Collections.Generic;
namespace Memoria.Accessibility.Navigation
{
    // A reviewed region bound to the person it talks to is not listed separately, so its own current Story
    // evidence travels with that person. A union only: the region never removes or replaces the person's
    // own milestones, action or current step.
    public static class FieldBoundStory
    {
        public static void Merge(FieldTargetObservation region,FieldTargetObservation person)
        {
            if(region==null || person==null)return;
            person.StoryMilestones=Union(person.StoryMilestones,region.StoryMilestones);
            person.NextFields=Union(person.NextFields,region.NextFields);
            if(person.StoryAction==null)person.StoryAction=region.StoryAction;
            if(person.StoryStepMilestone==Int32.MinValue && region.StoryStepMilestone!=Int32.MinValue){person.StoryStepMilestone=region.StoryStepMilestone;person.StoryVerb=region.StoryVerb;}
        }
        private static int[] Union(int[] own,int[] added)
        {
            if(added==null || added.Length==0)return own;
            var values=new List<int>();if(own!=null)values.AddRange(own);
            foreach(int value in added)if(!values.Contains(value))values.Add(value);
            return values.ToArray();
        }
    }
}
