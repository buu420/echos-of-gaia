using System;
using System.IO;
using System.Security.Cryptography;
namespace Memoria.Accessibility.Navigation
{
    public static class SpatialCueAssets
    {
        public static float[][] Load(string directory,string library)
        {
            Verify(library,IntPtr.Size==4?16537600:29447776,IntPtr.Size==4?"b4ac8eb6e0e06d63d25daca9e6c37ab7748431833ad4706bbcf813d7b2ace9a7":"45bd778a10e909b926c20a1ed55f08255fd573d11c05c625bacd4d6ef368eed5");
            string[] files={"field-exit.wav","field-npc.wav","field-interactable.wav"};
            string[] hashes={"6de6494a2e074401942c358035a96ad2ce0b9c70f58a7d6a0016101283f28924","4dd8dd5db53b171794cecd82f034c1b41026f22ef1346a69e9bd582db3d5abb4","6d7d573a5e2d9dcd8546690c40ab0708adda2c36d6a09393132518031110c5ed"};
            int[] sizes={143046,24044,48044};var clips=new float[3][];
            for(int i=0;i<3;i++){
                string path=Path.Combine(directory,files[i]);Verify(path,sizes[i],hashes[i]);byte[] bytes=File.ReadAllBytes(path);ValidateWave(bytes);
                using(var hash=SHA256.Create())if(!String.Equals(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-",""),hashes[i],StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Proximity recording changed while loading: "+files[i]);
                clips[i]=new float[(bytes.Length-44)/2];for(int n=0;n<clips[i].Length;n++)clips[i][n]=(short)(bytes[44+2*n]|bytes[45+2*n]<<8)/32768f;
            }
            return clips;
        }
        private static bool Label(byte[] data,int offset,string value){for(int i=0;i<value.Length;i++)if(data[offset+i]!=value[i])return false;return true;}
        private static void ValidateWave(byte[] data)
        {
            if(data==null || data.Length<46 || (data.Length&1)!=0 || !BitConverter.IsLittleEndian || !Label(data,0,"RIFF") || BitConverter.ToInt32(data,4)!=data.Length-8 ||
                !Label(data,8,"WAVEfmt ") || BitConverter.ToInt32(data,16)!=16 || BitConverter.ToInt16(data,20)!=1 || BitConverter.ToInt16(data,22)!=1 ||
                BitConverter.ToInt32(data,24)!=24000 || BitConverter.ToInt32(data,28)!=48000 || BitConverter.ToInt16(data,32)!=2 || BitConverter.ToInt16(data,34)!=16 ||
                !Label(data,36,"data") || BitConverter.ToInt32(data,40)!=data.Length-44)throw new InvalidOperationException("Proximity recording is not canonical 24 kHz mono PCM16");
        }
        private static void Verify(string path,long bytes,string expected)
        {
            if(new FileInfo(path).Length!=bytes)throw new InvalidOperationException("Proximity asset size differs: "+Path.GetFileName(path));
            using(var source=File.OpenRead(path))using(var hash=SHA256.Create())if(!String.Equals(BitConverter.ToString(hash.ComputeHash(source)).Replace("-",""),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Proximity asset hash differs: "+Path.GetFileName(path));
        }
    }
}
