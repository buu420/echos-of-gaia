using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Memoria.Accessibility.AudioDescriptions;
namespace FF9Access.Tests
{
    internal static class DescriptionBankTests
    {
        private static byte[] Wave()
        {
            var bytes=new byte[48044];Action<int,int> put=(offset,value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,bytes,offset,4);
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("RIFF"),0,bytes,0,4);put(4,bytes.Length-8);Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "),0,bytes,8,8);
            put(16,16);bytes[20]=1;bytes[22]=1;put(24,24000);put(28,48000);bytes[32]=2;bytes[34]=16;Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("data"),0,bytes,36,4);put(40,48000);return bytes;
        }
        private static DescriptionAudioSpec Spec(string id,byte[] wave){using(var hash=SHA256.Create())return new DescriptionAudioSpec(id,id+".wav",BitConverter.ToString(hash.ComputeHash(wave)).Replace("-","").ToLowerInvariant(),wave.Length,1);}
        public static void PublishesOnlyACompleteVerifiedBankOnTheOwnedWorker()
        {
            Action work=null;var bank=new DescriptionAudioBank(run=>work=run);var wave=Wave();int reads=0;
            bank.Prepare(1,new[]{Spec("one",wave),Spec("two",wave)},()=>true,name=>{reads++;return wave;});
            TestAssert.Equal(0,reads);byte[] result;TestAssert.False(bank.TryGet(1,"one",out result));work();
            TestAssert.True(bank.TryGet(1,"two",out result));TestAssert.Equal(2,reads);TestAssert.True(bank.SourceVerified(1));
        }
        public static void ACompletedOldWorkerCannotPublishIntoANewMovie()
        {
            var work=new List<Action>();var bank=new DescriptionAudioBank(run=>work.Add(run));var wave=Wave();
            bank.Prepare(1,new[]{Spec("one",wave)},()=>true,name=>wave);bank.Prepare(2,new[]{Spec("two",wave)},()=>true,name=>wave);
            work[0]();byte[] result;TestAssert.False(bank.TryGet(1,"one",out result));TestAssert.False(bank.TryGet(2,"one",out result));work[1]();TestAssert.True(bank.TryGet(2,"two",out result));
        }
        public static void SourceMismatchDoesNotAuthorizeCaptionOrVoiceFallback()
        {
            Action work=null;int reads=0;var bank=new DescriptionAudioBank(run=>work=run);var wave=Wave();bank.Prepare(1,new[]{Spec("one",wave)},()=>false,name=>{reads++;return wave;});work();
            byte[] result;TestAssert.False(bank.TryGet(1,"one",out result));TestAssert.False(bank.SourceVerified(1));TestAssert.Equal(0,reads);TestAssert.True(bank.Failure(1)!=null);
        }
        public static void CorruptClipRejectsTheBankButRetainsVerifiedSourceForSpeechFallback()
        {
            Action work=null;var bank=new DescriptionAudioBank(run=>work=run);var wave=Wave();var spec=Spec("one",wave);wave[100]=33;
            bank.Prepare(1,new[]{spec},()=>true,name=>wave);work();byte[] result;TestAssert.False(bank.TryGet(1,"one",out result));TestAssert.True(bank.SourceVerified(1));TestAssert.True(bank.Failure(1)!=null);
        }
        public static void ResetDuringAReadRejectsTheLateResult()
        {
            Action work=null;var bank=new DescriptionAudioBank(run=>work=run);var wave=Wave();bank.Prepare(1,new[]{Spec("one",wave)},()=>true,name=>{bank.Reset();return wave;});work();
            byte[] result;TestAssert.False(bank.TryGet(1,"one",out result));TestAssert.False(bank.SourceVerified(1));
        }
        public static void ManifestPathTraversalAndOversizeAudioAreRejectedBeforeQueueing()
        {
            int queued=0;var bank=new DescriptionAudioBank(run=>queued++);var wave=Wave();var spec=Spec("one",wave);
            bool failed=false;try{bank.Prepare(1,new[]{new DescriptionAudioSpec("one","../outside.wav",spec.SHA256,wave.Length,1)},()=>true,name=>wave);}catch(ArgumentException){failed=true;}TestAssert.True(failed);
            failed=false;try{bank.Prepare(1,new[]{new DescriptionAudioSpec("one","one.wav",spec.SHA256,40000000,1)},()=>true,name=>wave);}catch(ArgumentException){failed=true;}TestAssert.True(failed);TestAssert.Equal(0,queued);
        }
    }
}
