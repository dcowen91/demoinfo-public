﻿using DemoInfo.DT;
using DemoInfo.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DemoInfo.DP.Handler
{
    public static class PacketEntitesHandler
    {
		public static void Apply(PacketEntities packetEntities, IBitStream reader, DemoParser parser)
        {
			int currentEntity = -1;
			for (int i = 0; i < packetEntities.UpdatedEntries; i++) {
				currentEntity += 1 + (int)reader.ReadUBitInt();

				// Leave flag
				if (!reader.ReadBit()) {
					// enter flag
					if (reader.ReadBit()) {
						var e = ReadEnterPVS(reader, currentEntity, parser);

						parser.Entities[currentEntity] = e;

						e.ApplyUpdate(reader);
					} else {
						// preserve
						Entity e = parser.Entities[currentEntity];
						e.ApplyUpdate(reader);
					}
				} else {
					// leave
					parser.Entities [currentEntity].Leave ();
					parser.Entities[currentEntity] = null;
					if (reader.ReadBit()) {
					}
				}
			}
        }

        private static Entity ReadEnterPVS(IBitStream reader, int id, DemoParser parser)
        {
            int serverClassID = (int)reader.ReadInt(parser.SendTableParser.ClassBits);

            ServerClass entityClass = parser.SendTableParser.ServerClasses[serverClassID];

            reader.ReadInt(10); //Entity serial. 

			Entity newEntity = new Entity(id, entityClass);

			newEntity.ServerClass.AnnounceNewEntity(newEntity);

			object[] fastBaseline;
			if (parser.PreprocessedBaselines.TryGetValue(serverClassID, out fastBaseline))
				PropertyEntry.Emit(newEntity, fastBaseline);
			else {
				var preprocessedBaseline = new List<object>();
				if (parser.instanceBaseline.ContainsKey(serverClassID))
					using (var collector = new PropertyCollector(newEntity, preprocessedBaseline))
					using (var bitStream = BitStreamUtil.Create(parser.instanceBaseline[serverClassID]))
						newEntity.ApplyUpdate(bitStream);

				parser.PreprocessedBaselines.Add(serverClassID, preprocessedBaseline.ToArray());
			}

            return newEntity;
        }

		private class PropertyCollector : IDisposable
		{
			private readonly Entity Underlying;
			private readonly IList<object> Capture;

			public PropertyCollector(Entity underlying, IList<object> capture)
			{
				Underlying = underlying;
				Capture = capture;

				foreach (var prop in Underlying.Props) {
					switch (prop.Entry.Prop.Type) {
					case SendPropertyType.Array:
						prop.ArrayRecived += HandleArrayRecived;
						break;
					case SendPropertyType.Float:
						prop.FloatRecived += HandleFloatRecived;
						break;
					case SendPropertyType.Int:
						prop.IntRecived += HandleIntRecived;
						break;
					case SendPropertyType.String:
						prop.StringRecived += HandleStringRecived;
						break;
					case SendPropertyType.Vector:
					case SendPropertyType.VectorXY:
						prop.VectorRecived += HandleVectorRecived;
						break;
					default:
						throw new NotImplementedException();
					}
				}
			}

			private void HandleVectorRecived (object sender, PropertyUpdateEventArgs<Vector> e) { Capture.Add(e.Record()); }
			private void HandleStringRecived (object sender, PropertyUpdateEventArgs<string> e) { Capture.Add(e.Record()); }
			private void HandleIntRecived (object sender, PropertyUpdateEventArgs<int> e) { Capture.Add(e.Record()); }
			private void HandleFloatRecived (object sender, PropertyUpdateEventArgs<float> e) { Capture.Add(e.Record()); }
			private void HandleArrayRecived (object sender, PropertyUpdateEventArgs<object[]> e) { Capture.Add(e.Record()); }

			public void Dispose()
			{
				foreach (var prop in Underlying.Props) {
					switch (prop.Entry.Prop.Type) {
					case SendPropertyType.Array:
						prop.ArrayRecived -= HandleArrayRecived;
						break;
					case SendPropertyType.Float:
						prop.FloatRecived -= HandleFloatRecived;
						break;
					case SendPropertyType.Int:
						prop.IntRecived -= HandleIntRecived;
						break;
					case SendPropertyType.String:
						prop.StringRecived -= HandleStringRecived;
						break;
					case SendPropertyType.Vector:
					case SendPropertyType.VectorXY:
						prop.VectorRecived -= HandleVectorRecived;
						break;
					default:
						throw new NotImplementedException();
					}
				}
			}
		}
    }
}
