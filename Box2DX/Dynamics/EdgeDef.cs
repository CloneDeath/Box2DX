using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Box2DX.Collision;
using Box2DX.Common;

namespace Box2DX.Dynamics
{
	/// <summary>
	/// This structure is used to build a chain of edges.
	/// </summary>
	public class EdgeDef : FixtureDef
	{
		public EdgeDef()
		{
			Type = ShapeType.EdgeShape;
		}

		/// <summary>
		/// The start vertex.
		/// </summary>
		public Vec2 Vertex1;

		/// <summary>
		/// The end vertex.
		/// </summary>
		public Vec2 Vertex2;
	}
}
