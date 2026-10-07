using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Blender UV seams reference original vertices, retaining rigid skinning and triangle winding.</summary>
    public static class CreatureSurfaceUV
    {
        public sealed class Mapping
        {
            public int[] sourceVertices;
            public Vector2[] uv;
            public ResourceCreatureMesh.Submesh[] submeshes;
        }

        public static string Family(string resource)=>resource switch
        {
            "MarsSkeleton"=>"Skeleton", "GreenStalker"=>"Xeno",
            "NoxaraClockworkSpider"=>"Spider", "KryonBear"=>"Ice",
            "MechanicalScorpion"=>"Machine", "GJMagentaGolem"=>"Sandstone",
            "PSRCrystalTitan"=>"Crystal", _=>null
        };

        public static Mapping Load(string resource,byte[] model,ResourceCreatureMesh.Data data)
        {
            if(Family(resource)==null)return null;
            TextAsset asset=Resources.Load<TextAsset>(resource+"/SkinUV");
            if(asset==null)throw new InvalidDataException("Missing Blender UV asset: "+resource);
            using var zip=new GZipStream(new MemoryStream(asset.bytes),CompressionMode.Decompress);
            using var reader=new BinaryReader(zip);
            if(reader.ReadUInt32()!=0x56554644||reader.ReadUInt32()!=1)
                throw new InvalidDataException("Invalid Blender UV header: "+resource);
            int originalCount=reader.ReadInt32(),count=reader.ReadInt32(),slots=reader.ReadInt32();
            if(originalCount!=data.boneIndices.Length||count<=0||count>1000000||slots!=data.submeshes.Length)
                throw new InvalidDataException("Blender UV dimensions do not match: "+resource);
            byte[] sourceHash=reader.ReadBytes(32);
            using var sha=SHA256.Create();
            if(Convert.ToBase64String(sourceHash)!=Convert.ToBase64String(sha.ComputeHash(model)))
                throw new InvalidDataException("Blender UV asset is stale; rebuild surfaces for: "+resource);
            var result=new Mapping{sourceVertices=new int[count],uv=new Vector2[count],
                submeshes=new ResourceCreatureMesh.Submesh[slots]};
            for(int i=0;i<count;i++)
            {
                int original=reader.ReadInt32();float u=reader.ReadSingle(),v=reader.ReadSingle();
                if(original<0||original>=originalCount||!float.IsFinite(u)||!float.IsFinite(v))
                    throw new InvalidDataException("Invalid Blender UV vertex: "+resource);
                result.sourceVertices[i]=original;result.uv[i]=new Vector2(u,v);
            }
            for(int slot=0;slot<slots;slot++)
            {
                int length=reader.ReadInt32();
                if(length!=data.submeshes[slot].triangles.Length)
                    throw new InvalidDataException("Blender UV changed triangle count: "+resource);
                int[] indices=new int[length];
                for(int i=0;i<length;i++)
                {
                    int index=reader.ReadInt32();
                    if(index<0||index>=count||result.sourceVertices[index]!=data.submeshes[slot].triangles[i])
                        throw new InvalidDataException("Blender UV changed original geometry: "+resource);
                    indices[i]=index;
                }
                result.submeshes[slot]=new ResourceCreatureMesh.Submesh{triangles=indices};
            }
            if(zip.ReadByte()!=-1)throw new InvalidDataException("Trailing Blender UV data: "+resource);
            return result;
        }
    }
}
