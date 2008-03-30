﻿/*
  Box2DX Copyright (c) 2008 Ihar Kalasouski http://code.google.com/p/box2dx
  Box2D original C++ version Copyright (c) 2006-2007 Erin Catto http://www.gphysics.com

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

using System;
using System.Collections.Generic;
using System.Text;

using Box2DX.Common;
using Box2DX.Collision;

namespace Box2DX.Dynamics
{
	public struct TimeStep
	{
		public float Dt; // time step
		public float Inv_Dt; // inverse time step (0 if dt == 0).
		public int MaxIterations;
	}

	/// <summary>
	/// The world class manages all physics entities, dynamic simulation,
	/// and asynchronous queries.
	/// </summary>
	public class World : IDisposable
	{
		public bool _lock;

		public BroadPhase _broadPhase;
		public ContactManager _contactManager;

		public Body _bodyList;
		public Joint _jointList;

		// Do not access
		public Contact _contactList;

		public int _bodyCount;
		public int _contactCount;
		public int _jointCount;

		public Vector2 _gravity;
		public Vector2 Gravity { get { return _gravity; } set { _gravity = value; } }

		public bool _allowSleep;

		public Body _groundBody;

		public DestructionListener _destructionListener;
		public BoundaryListener _boundaryListener;
		public ContactFilter _contactFilter;
		public ContactListener _contactListener;
		public DebugDraw _debugDraw;

		public int _positionIterationCount;

		// This is for debugging the solver.
		public static int s_enablePositionCorrection = 1;

		// This is for debugging the solver.
		public static int s_enableWarmStarting = 1;

		// This is for debugging the solver.
		public static int s_enableTOI = 1;

		/// <summary>
		/// Construct a world object.
		/// </summary>
		/// <param name="worldAABB">A bounding box that completely encompasses all your shapes.</param>
		/// <param name="gravity">The world gravity vector.</param>
		/// <param name="doSleep">Improve performance by not simulating inactive bodies.</param>
		public World(AABB worldAABB, Vector2 gravity, bool doSleep)
		{
			_destructionListener = null;
			_boundaryListener = null;
			_contactFilter = WorldCallback.DefaultFilter;
			_contactListener = null;
			_debugDraw = null;

			_bodyList = null;
			_contactList = null;
			_jointList = null;

			_bodyCount = 0;
			_contactCount = 0;
			_jointCount = 0;

			_allowSleep = doSleep;
			_gravity = gravity;

			_lock = false;

			_contactManager = new ContactManager();
			_contactManager._world = this;
			_broadPhase = new BroadPhase(worldAABB, _contactManager);

			BodyDef bd = new BodyDef();
			_groundBody = CreateStaticBody(bd);
		}

		/// <summary>
		/// Destruct the world. All physics entities are destroyed.
		/// </summary>
		public void Dispose()
		{
			DestroyBody(_groundBody);
			if (_broadPhase is IDisposable)
				(_broadPhase as IDisposable).Dispose();
			_broadPhase = null;
		}

		/// <summary>
		/// Register a destruction listener.
		/// </summary>
		/// <param name="listener"></param>
		public void SetListener(DestructionListener listener)
		{
			_destructionListener = listener;
		}

		/// <summary>
		/// Register a broad-phase boundary listener.
		/// </summary>
		/// <param name="listener"></param>
		public void SetListener(BoundaryListener listener)
		{
			_boundaryListener = listener;
		}

		/// <summary>
		/// Register a contact filter to provide specific control over collision.
		/// Otherwise the default filter is used (b2_defaultFilter).
		/// </summary>
		/// <param name="filter"></param>
		public void SetFilter(ContactFilter filter)
		{
			_contactFilter = filter;
		}

		/// <summary>
		/// Register a contact event listener
		/// </summary>
		/// <param name="listener"></param>
		public void SetListener(ContactListener listener)
		{
			_contactListener = listener;
		}

		/// <summary>
		/// Register a routine for debug drawing. The debug draw functions are called
		/// inside the World.Step method, so make sure your renderer is ready to
		/// consume draw commands when you call Step().
		/// </summary>
		/// <param name="debugDraw"></param>
		public void SetDebugDraw(DebugDraw debugDraw)
		{
			_debugDraw = debugDraw;
		}

		/// <summary>
		/// Create a static rigid body given a definition. No reference to the definition
		/// is retained.
		/// @warning This function is locked during callbacks.
		/// </summary>
		/// <param name="def"></param>
		/// <returns></returns>
		public Body CreateStaticBody(BodyDef def)
		{
			Box2DXDebug.Assert(_lock == false);
			if (_lock == true)
			{
				return null;
			}

			Body b = new Body(def, Body.BodyType.Static, this);

			// Add to world doubly linked list.
			b._prev = null;
			b._next = _bodyList;
			if (_bodyList!=null)
			{
				_bodyList._prev = b;
			}
			_bodyList = b;
			++_bodyCount;

			return b;
		}

		/// <summary>
		/// Create a dynamic rigid body given a definition. No reference to the definition
		/// is retained.
		/// @warning This function is locked during callbacks.
		/// </summary>
		/// <param name="def"></param>
		/// <returns></returns>
		public Body CreateDynamicBody(BodyDef def)
		{
			Box2DXDebug.Assert(_lock == false);
			if (_lock == true)
			{
				return null;
			}

			Body b = new Body(def, Body.BodyType.Dynamic, this);

			// Add to world doubly linked list.
			b._prev = null;
			b._next = _bodyList;
			if (_bodyList!=null)
			{
				_bodyList._prev = b;
			}
			_bodyList = b;
			++_bodyCount;

			return b;
		}

		/// <summary>
		/// Destroy a rigid body given a definition. No reference to the definition
		/// is retained. This function is locked during callbacks.
		/// @warning This automatically deletes all associated shapes and joints.
		/// @warning This function is locked during callbacks.
		/// </summary>
		/// <param name="b"></param>
		public void DestroyBody(Body b)
		{
			Box2DXDebug.Assert(_bodyCount > 0);
			Box2DXDebug.Assert(_lock == false);
			if (_lock == true)
			{
				return;
			}

			// Delete the attached joints.
			JointEdge jn = null;
			if (b._jointList != null)
				jn = b._jointList;
			while (jn != null)
			{
				JointEdge jn0 = jn;
				jn = jn.Next;

				if (_destructionListener!=null)
				{
					_destructionListener.SayGoodbye(jn0.Joint);
				}

				DestroyJoint(jn0.Joint);
			}

			// Delete the attached shapes. This destroys broad-phase
			// proxies and pairs, leading to the destruction of contacts.
			Shape s = null;
			if (b._shapeList != null)
				s = b._shapeList;
			while (s != null)
			{
				Shape s0 = s;
				s = s._next;

				if (_destructionListener!=null)
				{
					_destructionListener.SayGoodbye(s0);
				}

				s0.DestroyProxy(_broadPhase);
				Shape.Destroy(s0);
			}

			// Remove world body list.
			if (b._prev!=null)
			{
				b._prev._next = b._next;
			}

			if (b._next!=null)
			{
				b._next._prev = b._prev;
			}

			if (b == _bodyList)
			{
				_bodyList = b._next;
			}

			--_bodyCount;
			if (b is IDisposable)
				(b as IDisposable).Dispose();
			b = null;
		}

		/// <summary>
		/// Create a joint to constrain bodies together. No reference to the definition
		/// is retained. This may cause the connected bodies to cease colliding.
		/// @warning This function is locked during callbacks.
		/// </summary>
		/// <param name="def"></param>
		/// <returns></returns>
		public Joint CreateJoint(JointDef def)
		{
			Box2DXDebug.Assert(_lock == false);

			Joint j = Joint.Create(def);

			// Connect to the world list.
			j._prev = null;
			j._next = _jointList;
			if (_jointList != null)
			{
				_jointList._prev = j;
			}
			_jointList = j;
			++_jointCount;

			// Connect to the bodies' doubly linked lists.
			j._node1.Joint = j;
			j._node1.Other = j._body2;
			j._node1.Prev = null;
			j._node1.Next = j._body1._jointList;
			if (j._body1._jointList != null)
				j._body1._jointList.Prev = j._node1;
			j._body1._jointList = j._node1;

			j._node2.Joint = j;
			j._node2.Other = j._body1;
			j._node2.Prev = null;
			j._node2.Next = j._body2._jointList;
			if (j._body2._jointList != null)
				j._body2._jointList.Prev = j._node2;
			j._body2._jointList = j._node2;

			// If the joint prevents collisions, then reset collision filtering.
			if (def.CollideConnected == false)
			{
				// Reset the proxies on the body with the minimum number of shapes.
				Body b = def.Body1._shapeCount < def.Body2._shapeCount ? def.Body1 : def.Body2;
				for (Shape s = b._shapeList; s != null; s = s._next)
				{
					s.ResetProxy(_broadPhase, b._xf);
				}
			}

			return j;
		}

		/// <summary>
		/// Destroy a joint. This may cause the connected bodies to begin colliding.
		/// @warning This function is locked during callbacks.
		/// </summary>
		/// <param name="j"></param>
		public void DestroyJoint(Joint j)
		{
			Box2DXDebug.Assert(_lock == false);

			bool collideConnected = j._collideConnected;

			// Remove from the doubly linked list.
			if (j._prev != null)
			{
				j._prev._next = j._next;
			}

			if (j._next != null)
			{
				j._next._prev = j._prev;
			}

			if (j == _jointList)
			{
				_jointList = j._next;
			}

			// Disconnect from island graph.
			Body body1 = j._body1;
			Body body2 = j._body2;

			// Wake up connected bodies.
			body1.WakeUp();
			body2.WakeUp();

			// Remove from body 1.
			if (j._node1.Prev != null)
			{
				j._node1.Prev.Next = j._node1.Next;
			}

			if (j._node1.Next != null)
			{
				j._node1.Next.Prev = j._node1.Prev;
			}

			if (j._node1 == body1._jointList)
			{
				body1._jointList = j._node1.Next;
			}

			j._node1.Prev = null;
			j._node1.Next = null;

			// Remove from body 2
			if (j._node2.Prev != null)
			{
				j._node2.Prev.Next = j._node2.Next;
			}

			if (j._node2.Next != null)
			{
				j._node2.Next.Prev = j._node2.Prev;
			}

			if (j._node2 == body2._jointList)
			{
				body2._jointList = j._node2.Next;
			}

			j._node2.Prev = null;
			j._node2.Next = null;

			Joint.Destroy(j);

			Box2DXDebug.Assert(_jointCount > 0);
			--_jointCount;

			// If the joint prevents collisions, then reset collision filtering.
			if (collideConnected == false)
			{
				// Reset the proxies on the body with the minimum number of shapes.
				Body b = body1._shapeCount < body2._shapeCount ? body1 : body2;
				for (Shape s = b._shapeList; s != null; s = s._next)
				{
					s.ResetProxy(_broadPhase, b._xf);
				}
			}
		}

		/// <summary>
		/// The world provides a single static ground body with no collision shapes.
		/// You can use this to simplify the creation of joints and static shapes.
		/// </summary>
		/// <returns></returns>
		public Body GetGroundBody()
		{
			return _groundBody;
		}

		/// <summary>
		/// Get the world body list. With the returned body, use Body.GetNext to get
		/// the next body in the world list. A null body indicates the end of the list.
		/// </summary>
		/// <returns>The head of the world body list.</returns>
		public Body GetBodyList()
		{
			return _bodyList;
		}

		/// <summary>
		/// Get the world joint list. With the returned joint, use Joint.GetNext to get
		/// the next joint in the world list. A null joint indicates the end of the list.
		/// </summary>
		/// <returns>The head of the world joint list.</returns>
		public Joint GetJointList()
		{
			return _jointList;
		}

		/// <summary>
		/// Take a time step. This performs collision detection, integration,
		/// and constraint solution.
		/// </summary>
		/// <param name="dt">The amount of time to simulate, this should not vary.</param>
		/// <param name="iterations">The number of iterations to be used by the constraint solver.</param>
		public void Step(float dt, int iterations)
		{
			_lock = true;

			TimeStep step = new TimeStep();
			step.Dt = dt;
			step.MaxIterations = iterations;
			if (dt > 0.0f)
			{
				step.Inv_Dt = 1.0f / dt;
			}
			else
			{
				step.Inv_Dt = 0.0f;
			}

			// Update contacts.
			_contactManager.Collide();

			// Integrate velocities, solve velocity constraints, and integrate positions.
			if (step.Dt > 0.0f)
			{
				Solve(step);
			}

			// Handle TOI events.
			if (World.s_enableTOI!=0 && step.Dt > 0.0f)
			{
				SolveTOI(step);
			}

			// Draw debug information.
			DrawDebugData();

			_lock = false;
		}

		/// Query the world for all shapes that potentially overlap the
		/// provided AABB. You provide a shape pointer buffer of specified
		/// size. The number of shapes found is returned.
		/// @param aabb the query box.
		/// @param shapes a user allocated shape pointer array of size maxCount (or greater).
		/// @param maxCount the capacity of the shapes array.
		/// @return the number of shapes found in aabb.
		public int Query(AABB aabb, Shape[] shapes, int maxCount)
		{
			//using (object[] results = new object[maxCount])
			{
				object[] results = new object[maxCount];

				int count = _broadPhase.Query(aabb, results, maxCount);

				for (int i = 0; i < count; ++i)
				{
					shapes[i] = (Shape)results[i];
				}

				results = null;
				return count;
			}
		}

		// Find islands, integrate and solve constraints, solve position constraints
		internal void Solve(TimeStep step)
		{
			_positionIterationCount = 0;

			// Size the island for the worst case.
			Island island = new Island(_bodyCount, _contactCount, _jointCount, _contactListener);

			// Clear all the island flags.
			for (Body b = _bodyList; b != null; b = b._next)
			{
				b._flags &= ~Body.BodyFlags.Island;
			}
			for (Contact c = _contactList; c != null; c = c._next)
			{
				c._flags &= ~Contact.CollisionFlags.Island;
			}
			for (Joint j = _jointList; j != null; j = j._next)
			{
				j._islandFlag = false;
			}

			// Build and simulate all awake islands.
			int stackSize = _bodyCount;
			//using (Body[] stack = new Body[stackSize])
			{
				Body[] stack = new Body[stackSize];

				for (Body seed = _bodyList; seed != null; seed = seed._next)
				{
					if ((seed._flags & (Body.BodyFlags.Island | Body.BodyFlags.Sleep | Body.BodyFlags.Frozen)) != 0)
					{
						continue;
					}

					if (seed.IsStatic())
					{
						continue;
					}

					// Reset island and stack.
					island.Clear();
					int stackCount = 0;
					stack[stackCount++] = seed;
					seed._flags |= Body.BodyFlags.Island;

					// Perform a depth first search (DFS) on the constraint graph.
					while (stackCount > 0)
					{
						// Grab the next body off the stack and add it to the island.
						Body b = stack[--stackCount];
						island.Add(b);

						// Make sure the body is awake.
						b._flags &= ~Body.BodyFlags.Sleep;

						// To keep islands as small as possible, we don't
						// propagate islands across static bodies.
						if (b.IsStatic())
						{
							continue;
						}

						// Search all contacts connected to this body.
						for (ContactEdge cn = b._contactList; cn != null; cn = cn.Next)
						{
							// Has this contact already been added to an island?
							if ((cn.Contact._flags & (Contact.CollisionFlags.Island | Contact.CollisionFlags.NonSolid)) != 0)
							{
								continue;
							}

							// Is this contact touching?
							if (cn.Contact.GetManifoldCount() == 0)
							{
								continue;
							}

							island.Add(cn.Contact);
							cn.Contact._flags |= Contact.CollisionFlags.Island;

							Body other = cn.Other;

							// Was the other body already added to this island?
							if ((other._flags & Body.BodyFlags.Island) != 0)
							{
								continue;
							}

							Box2DXDebug.Assert(stackCount < stackSize);
							stack[stackCount++] = other;
							other._flags |= Body.BodyFlags.Island;
						}

						// Search all joints connect to this body.
						for (JointEdge jn = b._jointList; jn != null; jn = jn.Next)
						{
							if (jn.Joint._islandFlag == true)
							{
								continue;
							}

							island.Add(jn.Joint);
							jn.Joint._islandFlag = true;

							Body other = jn.Other;
							if ((other._flags & Body.BodyFlags.Island) != 0)
							{
								continue;
							}

							Box2DXDebug.Assert(stackCount < stackSize);
							stack[stackCount++] = other;
							other._flags |= Body.BodyFlags.Island;
						}
					}

					island.Solve(step, _gravity, World.s_enablePositionCorrection > 0, _allowSleep);
					_positionIterationCount = Common.Math.Max(_positionIterationCount, island._positionIterationCount);

					// Post solve cleanup.
					for (int i = 0; i < island._bodyCount; ++i)
					{
						// Allow static bodies to participate in other islands.
						Body b = island._bodies[i];
						if (b.IsStatic())
						{
							b._flags &= ~Body.BodyFlags.Island;
						}
					}
				}

				stack = null;
			}

			// Synchronize shapes, check for out of range bodies.
			for (Body b = _bodyList; b != null; b = b.GetNext())
			{
				if ((b._flags & (Body.BodyFlags.Sleep | Body.BodyFlags.Frozen))!=0)
				{
					continue;
				}

				if (b.IsStatic())
				{
					continue;
				}

				// Update shapes (for broad-phase). If the shapes go out of
				// the world AABB then shapes and contacts may be destroyed,
				// including contacts that are
				bool inRange = b.SynchronizeShapes();

				// Did the body's shapes leave the world?
				if (inRange == false && _boundaryListener != null)
				{
					_boundaryListener.Violation(b);
				}
			}

			// Commit shape proxy movements to the broad-phase so that new contacts are created.
			// Also, some contacts can be destroyed.
			_broadPhase.Commit();
		}

		// Find TOI contacts and solve them.
		internal void SolveTOI(TimeStep step)
		{
			// Reserve an island and a stack for TOI island solution.
			Island island = new Island(_bodyCount, Settings.MaxTOIContactsPerIsland, 0, _contactListener);
			int stackSize = _bodyCount;
			//using (Body[] stack = new Body[stackSize])
			{
				Body[] stack = new Body[stackSize];
				for (Body b = _bodyList; b != null; b = b._next)
				{
					b._flags &= ~Body.BodyFlags.Island;
					b._sweep.T0 = 0.0f;
				}

				for (Contact c = _contactList; c != null; c = c._next)
				{
					// Invalidate TOI
					c._flags &= ~(Contact.CollisionFlags.Toi | Contact.CollisionFlags.Island);
				}

				// Find TOI events and solve them.
				for (; ; )
				{
					// Find the first TOI.
					Contact minContact = null;
					float minTOI = 1.0f;

					for (Contact c = _contactList; c != null; c = c._next)
					{
						if ((c._flags & (Contact.CollisionFlags.Slow | Contact.CollisionFlags.NonSolid)) != 0)
						{
							continue;
						}

						// TODO_ERIN keep a counter on the contact, only respond to M TOIs per contact.

						float toi = 1.0f;
						if ((c._flags & Contact.CollisionFlags.Toi) != 0)
						{
							// This contact has a valid cached TOI.
							toi = c._toi;
						}
						else
						{
							// Compute the TOI for this contact.
							Shape s1_ = c.GetShape1();
							Shape s2_ = c.GetShape2();
							Body b1_ = s1_.GetBody();
							Body b2_ = s2_.GetBody();

							if ((b1_.IsStatic() || b1_.IsSleeping()) && (b2_.IsStatic() || b2_.IsSleeping()))
							{
								continue;
							}

							// Put the sweeps onto the same time interval.
							float t0 = b1_._sweep.T0;

							if (b1_._sweep.T0 < b2_._sweep.T0)
							{
								t0 = b2_._sweep.T0;
								b1_._sweep.Advance(t0);
							}
							else if (b2_._sweep.T0 < b1_._sweep.T0)
							{
								t0 = b1_._sweep.T0;
								b2_._sweep.Advance(t0);
							}

							Box2DXDebug.Assert(t0 < 1.0f);

							// Compute the time of impact.
							toi = Collision.Collision.TimeOfImpact(c._shape1, b1_._sweep, c._shape2, b2_._sweep);
							Box2DXDebug.Assert(0.0f <= toi && toi <= 1.0f);

							if (toi > 0.0f && toi < 1.0f)
							{
								toi = Common.Math.Min((1.0f - toi) * t0 + toi, 1.0f);
							}


							c._toi = toi;
							c._flags |= Contact.CollisionFlags.Toi;
						}

						if (Common.Math.FLOAT32_EPSILON < toi && toi < minTOI)
						{
							// This is the minimum TOI found so far.
							minContact = c;
							minTOI = toi;
						}
					}

					if (minContact == null || 1.0f - 100.0f * Common.Math.FLOAT32_EPSILON < minTOI)
					{
						// No more TOI events. Done!
						break;
					}

					// Advance the bodies to the TOI.
					Shape s1 = minContact.GetShape1();
					Shape s2 = minContact.GetShape2();
					Body b1 = s1.GetBody();
					Body b2 = s2.GetBody();
					b1.Advance(minTOI);
					b2.Advance(minTOI);

					// The TOI contact likely has some new contact points.
					minContact.Update(_contactListener);
					minContact._flags &= ~Contact.CollisionFlags.Toi;

					if (minContact.GetManifoldCount() == 0)
					{
						// This shouldn't happen. Numerical error?
						//b2Assert(false);
						continue;
					}

					// Build the TOI island. We need a dynamic seed.
					Body seed = b1;
					if (seed.IsStatic())
					{
						seed = b2;
					}

					// Reset island and stack.
					island.Clear();
					int stackCount = 0;
					stack[stackCount++] = seed;
					seed._flags |= Body.BodyFlags.Island;

					// Perform a depth first search (DFS) on the contact graph.
					while (stackCount > 0)
					{
						// Grab the next body off the stack and add it to the island.
						Body b = stack[--stackCount];
						island.Add(b);

						// Make sure the body is awake.
						b._flags &= ~Body.BodyFlags.Sleep;

						// To keep islands as small as possible, we don't
						// propagate islands across static bodies.
						if (b.IsStatic())
						{
							continue;
						}

						// Search all contacts connected to this body.
						for (ContactEdge cn = b._contactList; cn != null; cn = cn.Next)
						{
							// Does the TOI island still have space for contacts?
							if (island._contactCount == island._contactCapacity)
							{
								continue;
							}

							// Has this contact already been added to an island? Skip slow or non-solid contacts.
							if ((cn.Contact._flags & (Contact.CollisionFlags.Island | Contact.CollisionFlags.Slow | Contact.CollisionFlags.NonSolid))!=0)
							{
								continue;
							}

							// Is this contact touching? For performance we are not updating this contact.
							if (cn.Contact.GetManifoldCount() == 0)
							{
								continue;
							}

							island.Add(cn.Contact);
							cn.Contact._flags |= Contact.CollisionFlags.Island;

							// Update other body.
							Body other = cn.Other;

							// Was the other body already added to this island?
							if ((other._flags & Body.BodyFlags.Island) != 0)
							{
								continue;
							}

							// March forward, this can do no harm since this is the min TOI.
							if (other.IsStatic() == false)
							{
								other.Advance(minTOI);
								other.WakeUp();
							}

							Box2DXDebug.Assert(stackCount < stackSize);
							stack[stackCount++] = other;
							other._flags |= Body.BodyFlags.Island;
						}
					}

					TimeStep subStep = new TimeStep();
					subStep.Dt = (1.0f - minTOI) * step.Dt;
					Box2DXDebug.Assert(subStep.Dt > Common.Math.FLOAT32_EPSILON);
					subStep.Inv_Dt = 1.0f / subStep.Dt;
					subStep.MaxIterations = step.MaxIterations;

					island.SolveTOI(subStep);

					// Post solve cleanup.
					for (int i = 0; i < island._bodyCount; ++i)
					{
						// Allow bodies to participate in future TOI islands.
						Body b = island._bodies[i];
						b._flags &= ~Body.BodyFlags.Island;

						if ((b._flags & (Body.BodyFlags.Sleep | Body.BodyFlags.Frozen)) != 0)
						{
							continue;
						}

						if (b.IsStatic())
						{
							continue;
						}

						// Update shapes (for broad-phase). If the shapes go out of
						// the world AABB then shapes and contacts may be destroyed,
						// including contacts that are
						bool inRange = b.SynchronizeShapes();

						// Did the body's shapes leave the world?
						if (inRange == false && _boundaryListener != null)
						{
							_boundaryListener.Violation(b);
						}

						// Invalidate all contact TOIs associated with this body. Some of these
						// may not be in the island because they were not touching.
						for (ContactEdge cn = b._contactList; cn!=null; cn = cn.Next)
						{
							cn.Contact._flags &= ~Contact.CollisionFlags.Toi;
						}
					}

					for (int i = 0; i < island._contactCount; ++i)
					{
						// Allow contacts to participate in future TOI islands.
						Contact c = island._contacts[i];
						c._flags &= ~(Contact.CollisionFlags.Toi | Contact.CollisionFlags.Island);
					}

					// Commit shape proxy movements to the broad-phase so that new contacts are created.
					// Also, some contacts can be destroyed.
					_broadPhase.Commit();
				}

				stack = null;
			}
		}

		internal void DrawJoint(Joint joint)
		{
			Body b1 = joint.GetBody1();
			Body b2 = joint.GetBody2();
			XForm xf1 = b1.GetXForm();
			XForm xf2 = b2.GetXForm();
			Vector2 x1 = xf1.Position;
			Vector2 x2 = xf2.Position;
			Vector2 p1 = joint.Anchor1;
			Vector2 p2 = joint.Anchor2;

			Color color = new Color(0.5f, 0.8f, 0.8f);

			switch (joint.GetType())
			{
				case JointType.DistanceJoint:
					_debugDraw.DrawSegment(p1, p2, color);
					break;

				case JointType.PulleyJoint:
					{
						PulleyJoint pulley = (PulleyJoint)joint;
						Vector2 s1 = pulley.GroundAnchor1;
						Vector2 s2 = pulley.GroundAnchor2;
						_debugDraw.DrawSegment(s1, p1, color);
						_debugDraw.DrawSegment(s2, p2, color);
						_debugDraw.DrawSegment(s1, s2, color);
					}
					break;

				case JointType.MouseJoint:
					// don't draw this
					break;

				default:
					_debugDraw.DrawSegment(x1, p1, color);
					_debugDraw.DrawSegment(p1, p2, color);
					_debugDraw.DrawSegment(x2, p2, color);
					break;
			}
		}

		internal void DrawShape(Shape shape, XForm xf, Color color, bool core)
		{
			Color coreColor = new Color(0.9f, 0.6f, 0.6f);

			switch (shape._type)
			{
				case ShapeType.CircleShape:
					{
						CircleShape circle = (CircleShape)shape;

						Vector2 center = Common.Math.Mul(xf, circle.GetLocalPosition());
						float radius = circle.GetRadius();
						Vector2 axis = xf.R.Col1;

						_debugDraw.DrawSolidCircle(center, radius, axis, color);

						if (core)
						{
							_debugDraw.DrawCircle(center, radius - Settings.ToiSlop, coreColor);
						}
					}
					break;

				case ShapeType.PolygonShape:
					{
						PolygonShape poly = (PolygonShape)shape;
						int vertexCount = poly.VertexCount;
						Vector2[] localVertices = poly.GetVertices();

						Box2DXDebug.Assert(vertexCount <= Settings.MaxPolygonVertices);
						Vector2[] vertices = new Vector2[Settings.MaxPolygonVertices];

						for (int i = 0; i < vertexCount; ++i)
						{
							vertices[i] = Common.Math.Mul(xf, localVertices[i]);
						}

						_debugDraw.DrawSolidPolygon(vertices, vertexCount, color);

						if (core)
						{
							Vector2[] localCoreVertices = poly.GetCoreVertices();
							for (int i = 0; i < vertexCount; ++i)
							{
								vertices[i] = Common.Math.Mul(xf, localCoreVertices[i]);
							}
							_debugDraw.DrawPolygon(vertices, vertexCount, coreColor);
						}
					}
					break;
			}
		}

		internal void DrawDebugData()
		{
			if (_debugDraw == null)
			{
				return;
			}

			DebugDraw.DrawFlags flags = _debugDraw.Flags;

			if ((flags & DebugDraw.DrawFlags.Shape) != 0)
			{
				bool core = (flags & DebugDraw.DrawFlags.CoreShape) == DebugDraw.DrawFlags.CoreShape;

				for (Body b = _bodyList; b != null; b = b.GetNext())
				{
					XForm xf = b.GetXForm();
					for (Shape s = b.GetShapeList(); s != null; s = s.GetNext())
					{
						if (b.IsStatic())
						{
							DrawShape(s, xf, new Color(0.5f, 0.9f, 0.5f), core);
						}
						else if (b.IsSleeping())
						{
							DrawShape(s, xf, new Color(0.5f, 0.5f, 0.9f), core);
						}
						else
						{
							DrawShape(s, xf, new Color(0.9f, 0.9f, 0.9f), core);
						}
					}
				}
			}

			if ((flags & DebugDraw.DrawFlags.Joint) != 0)
			{
				for (Joint j = _jointList; j != null; j = j.GetNext())
				{
					if (j.GetType() != JointType.MouseJoint)
					{
						DrawJoint(j);
					}
				}
			}

			if ((flags & DebugDraw.DrawFlags.Pair) != 0)
			{
				BroadPhase bp = _broadPhase;
				Vector2 invQ = new Vector2();
				invQ.Set(1.0f / bp._quantizationFactor.X, 1.0f / bp._quantizationFactor.Y);
				Color color = new Color(0.9f, 0.9f, 0.3f);

				for (int i = 0; i < PairManager.TableCapacity; ++i)
				{
					ushort index = bp._pairManager._hashTable[i];
					while (index != PairManager.NullPair)
					{
						Pair pair = bp._pairManager._pairs[index];
						Proxy p1 = bp._proxyPool[pair.ProxyId1];
						Proxy p2 = bp._proxyPool[pair.ProxyId2];

						AABB b1 = new AABB(), b2 = new AABB();
						b1.LowerBound.X = bp._worldAABB.LowerBound.X + invQ.X * bp._bounds[0][p1.LowerBounds[0]].Value;
						b1.LowerBound.Y = bp._worldAABB.LowerBound.Y + invQ.Y * bp._bounds[1][p1.LowerBounds[1]].Value;
						b1.UpperBound.X = bp._worldAABB.LowerBound.X + invQ.X * bp._bounds[0][p1.UpperBounds[0]].Value;
						b1.UpperBound.Y = bp._worldAABB.LowerBound.Y + invQ.Y * bp._bounds[1][p1.UpperBounds[1]].Value;
						b2.LowerBound.X = bp._worldAABB.LowerBound.X + invQ.X * bp._bounds[0][p2.LowerBounds[0]].Value;
						b2.LowerBound.Y = bp._worldAABB.LowerBound.Y + invQ.Y * bp._bounds[1][p2.LowerBounds[1]].Value;
						b2.UpperBound.X = bp._worldAABB.LowerBound.X + invQ.X * bp._bounds[0][p2.UpperBounds[0]].Value;
						b2.UpperBound.Y = bp._worldAABB.LowerBound.Y + invQ.Y * bp._bounds[1][p2.UpperBounds[1]].Value;

						Vector2 x1 = 0.5f * (b1.LowerBound + b1.UpperBound);
						Vector2 x2 = 0.5f * (b2.LowerBound + b2.UpperBound);

						_debugDraw.DrawSegment(x1, x2, color);

						index = pair.Next;
					}
				}
			}

			if ((flags & DebugDraw.DrawFlags.Aabb) != 0)
			{
				BroadPhase bp = _broadPhase;
				Vector2 worldLower = bp._worldAABB.LowerBound;
				Vector2 worldUpper = bp._worldAABB.UpperBound;

				Vector2 invQ = new Vector2();
				invQ.Set(1.0f / bp._quantizationFactor.X, 1.0f / bp._quantizationFactor.Y);
				Color color = new Color(0.9f, 0.3f, 0.9f);
				for (int i = 0; i < Settings.MaxProxies; ++i)
				{
					Proxy p = bp._proxyPool[i];
					if (p.IsValid == false)
					{
						continue;
					}

					AABB b = new AABB();
					b.LowerBound.X = worldLower.X + invQ.X * bp._bounds[0][p.LowerBounds[0]].Value;
					b.LowerBound.Y = worldLower.Y + invQ.Y * bp._bounds[1][p.LowerBounds[1]].Value;
					b.UpperBound.X = worldLower.X + invQ.X * bp._bounds[0][p.UpperBounds[0]].Value;
					b.UpperBound.Y = worldLower.Y + invQ.Y * bp._bounds[1][p.UpperBounds[1]].Value;

					Vector2[] vs1 = new Vector2[4];
					vs1[0].Set(b.LowerBound.X, b.LowerBound.Y);
					vs1[1].Set(b.UpperBound.X, b.LowerBound.Y);
					vs1[2].Set(b.UpperBound.X, b.UpperBound.Y);
					vs1[3].Set(b.LowerBound.X, b.UpperBound.Y);

					_debugDraw.DrawPolygon(vs1, 4, color);
				}

				Vector2[] vs = new Vector2[4];
				vs[0].Set(worldLower.X, worldLower.Y);
				vs[1].Set(worldUpper.X, worldLower.Y);
				vs[2].Set(worldUpper.X, worldUpper.Y);
				vs[3].Set(worldLower.X, worldUpper.Y);
				_debugDraw.DrawPolygon(vs, 4, new Color(0.3f, 0.9f, 0.9f));
			}

			if ((flags & DebugDraw.DrawFlags.Obb) != 0)
			{
				Color color = new Color(0.5f, 0.3f, 0.5f);

				for (Body b = _bodyList; b != null; b = b.GetNext())
				{
					XForm xf = b.GetXForm();
					for (Shape s = b.GetShapeList(); s != null; s = s.GetNext())
					{
						if (s.GetType() != ShapeType.PolygonShape)
						{
							continue;
						}

						PolygonShape poly = (PolygonShape)s;
						OBB obb = poly.GetOBB();
						Vector2 h = obb.Extents;
						Vector2[] vs = new Vector2[4];
						vs[0].Set(-h.X, -h.Y);
						vs[1].Set(h.X, -h.Y);
						vs[2].Set(h.X, h.Y);
						vs[3].Set(-h.X, h.Y);

						for (int i = 0; i < 4; ++i)
						{
							vs[i] = obb.Center + Common.Math.Mul(obb.R, vs[i]);
							vs[i] = Common.Math.Mul(xf, vs[i]);
						}

						_debugDraw.DrawPolygon(vs, 4, color);
					}
				}
			}

			if ((flags & DebugDraw.DrawFlags.CenterOfMass) != 0)
			{
				for (Body b = _bodyList; b != null; b = b.GetNext())
				{
					XForm xf = b.GetXForm();
					xf.Position = b.GetWorldCenter();
					_debugDraw.DrawXForm(xf);
				}
			}
		}
	}
}
