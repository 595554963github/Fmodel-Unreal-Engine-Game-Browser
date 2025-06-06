using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Objects.PhysicsEngine
{
    public class UPhysicsAsset : Assets.Exports.UObject
    {
        public int[] BoundsBodies = Array.Empty<int>();
        public FPackageIndex[] SkeletalBodySetups = Array.Empty<FPackageIndex>();
        public FPackageIndex[] ConstraintSetup = Array.Empty<FPackageIndex>();
        public Dictionary<FRigidBodyIndexPair, bool>? CollisionDisableTable;

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);

            BoundsBodies = GetOrDefault(nameof(BoundsBodies), Array.Empty<int>());
            SkeletalBodySetups = GetOrDefault(nameof(SkeletalBodySetups), Array.Empty<FPackageIndex>());
            ConstraintSetup = GetOrDefault(nameof(ConstraintSetup), Array.Empty<FPackageIndex>());

            var numRows = Ar.Read<int>();
            CollisionDisableTable = new Dictionary<FRigidBodyIndexPair, bool>(numRows);
            for (var i = 0; i < numRows; i++)
            {
                var rowKey = new FRigidBodyIndexPair(Ar);
                CollisionDisableTable[rowKey] = Ar.ReadBoolean();
            }
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            writer.WritePropertyName("CollisionDisableTable");
            writer.WriteStartArray();

            if (CollisionDisableTable != null)
            {
                foreach (var entry in CollisionDisableTable)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Key");
                    serializer.Serialize(writer, entry.Key);
                    writer.WritePropertyName("Value");
                    writer.WriteValue(entry.Value);
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
        }
    }

    public class FRigidBodyIndexPair
    {
        public readonly int[] Indices = new int[2];

        public FRigidBodyIndexPair(FArchive Ar)
        {
            Indices[0] = Ar.Read<int>();
            Indices[1] = Ar.Read<int>();
        }
    }
}