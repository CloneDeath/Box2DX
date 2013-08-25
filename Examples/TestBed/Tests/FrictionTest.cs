/*
  Box2DX Copyright (c) 2009 Ihar Kalasouski http://code.google.com/p/box2dx
  Box2D original C++ version Copyright (c) 2006-2009 Erin Catto http://www.gphysics.com

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.
*/

using Box2DX.Dynamics;
using Box2DX.Common;

namespace TestBed
{
	public class FrictionTest : Test
	{
		public FrictionTest()
		{
			// Define the ground body.
			BodyDef groundBodyDef = new BodyDef();
			groundBodyDef.Position.Set(0.0f, -10.0f);

			// Call the body factory which creates the ground box shape.
			// The body is also added to the world.
			Body groundBody = _world.CreateBody(groundBodyDef);

			// Define the ground box shape.
			PolygonDef groundShapeDef = new PolygonDef();

			// The extents are the half-widths of the box.
			groundShapeDef.SetAsBox(50.0f, 10.0f);

			// Add the ground shape to the ground body.
			groundBody.CreateFixture(groundShapeDef);

			// Define the dynamic body. We set its position and call the body factory.
			BodyDef bodyDef = new BodyDef();
			bodyDef.Position.Set(1.0f, 2.0f);
			Body bodyfast = _world.CreateBody(bodyDef);

			// Define another box shape for our dynamic body.
			PolygonDef shapeDef = new PolygonDef();
			shapeDef.SetAsBox(1.0f, 1.0f);
			shapeDef.Density = 1.0f;
			shapeDef.Friction = 0.3f;
			bodyfast.CreateFixture(shapeDef);
			bodyfast.SetMassFromShapes();

			bodyDef.Position.Set(-1.0f, 2.0f);
			Body bodyslow = _world.CreateBody(bodyDef);
			shapeDef.Friction = 0.8f;
			bodyslow.CreateFixture(shapeDef);
			bodyslow.SetMassFromShapes();

			bodyslow.SetLinearVelocity(new Vec2(-3, 0));
			bodyfast.SetLinearVelocity(new Vec2(3, 0));
		}

		public static Test Create()
		{
			return new FrictionTest();
		}
	}
}
