using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Box2DX.Common;
using Box2DX.Collision;

namespace Box2DX.Dynamics
{
	/// <summary>
	/// This structure is used to build a fixture with a circle shape.
	/// </summary>
	public class CircleDef : FixtureDef
	{
		public Vec2 LocalPosition;
		public float Radius;

		public CircleDef()
		{
			Type = ShapeType.CircleShape;
			LocalPosition = Vec2.Zero;
			Radius = 1.0f;
		}
	}
}
