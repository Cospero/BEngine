﻿using BEngine;
using MagicPhysX;
using System.Collections.Concurrent;
using System.Text;
using static BEngine.Collider;
using static MagicPhysX.NativeMethods;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;
using Vector2 = System.Numerics.Vector2;

namespace BEngineCore
{
	public class PhysicsEntity
	{
		public unsafe PxActor* Actor;
		public unsafe PxShape* Shape;
		public unsafe PxMaterial* Material;
		public unsafe PxTransform Transform;
		public ColliderType ColliderType;
		public Vector3 Velocity;
		public Vector3 AngularVelocity;
		public bool Dynamic;
		public bool Kinematic;
	}

	public struct ChangeActorScale
	{
		public PhysicsEntity Entity;
		public Vector3 Scale;
	}

	public struct ChangeActorVelocity
	{
		public PhysicsEntity Entity;
		public Vector3 Velocity;
	}

	public struct ChangeActorForce
	{
		public PhysicsEntity Entity;
		public Vector3 Force;
		public PxForceMode Mode;
	}

	public class Physics
	{
		private unsafe PxFoundation* foundation;
		private unsafe PxPhysics* physics;
		private unsafe PxScene* scene;
		private unsafe PxDefaultCpuDispatcher* dispatcher;
		private unsafe PxMaterial* material;

		private bool running = true;

		private const float FixedFrames = 50;
		private const float ScaleIncrease = 2;
		private const float FallDecreaseScale = 10f;

		private List<PhysicsEntity> _addActors = new();
		private List<PhysicsEntity> _removeActors = new();
		private List<PhysicsEntity> _swipeActors = new();
		private List<PhysicsEntity> _changeKinematic = new();
		private List<PhysicsEntity> _applyTransform = new();
		private List<ChangeActorForce> _applyForce = new();
		private List<ChangeActorForce> _applyTorque = new();
		private List<ChangeActorVelocity> _applyVelocity = new();
		private List<ChangeActorVelocity> _applyAngularVelocity = new();

		public ConcurrentDictionary<string, PhysicsEntity> Actors = new();

		private const float Gravity = -9.81f;

		public Physics()
		{
			// left impl Fix Capsule rotation, Fix Plane Size, Collisions, RaycastLayers, Raycasts

			// changing sizes in UpdateScale by passing object[] with values goten from indivual Collider.GetUpdateValues();
		}

		public unsafe void Initialize()
		{
			foundation = physx_create_foundation();

			PxPvd* pvd = phys_PxCreatePvd(foundation);
			string host = "127.0.0.1";
			int port = 5425;
			uint timeout = 100;

			fixed (byte* hostName = Encoding.UTF8.GetBytes(host))
			{
				PxPvdTransport* transport = phys_PxDefaultPvdSocketTransportCreate(hostName, port, timeout);
				bool connect = pvd->ConnectMut(transport, PxPvdInstrumentationFlags.All);
				if (connect == false)
				{
					Console.WriteLine("PVD connection error");
				}
				else
				{
					Console.WriteLine("PVD connected successfully!");
				}
			}

			// create physics
			uint PX_PHYSICS_VERSION_MAJOR = 5;
			uint PX_PHYSICS_VERSION_MINOR = 1;
			uint PX_PHYSICS_VERSION_BUGFIX = 3;
			uint versionNumber = (PX_PHYSICS_VERSION_MAJOR << 24) + (PX_PHYSICS_VERSION_MINOR << 16) + (PX_PHYSICS_VERSION_BUGFIX << 8);

			var tolerancesScale = new PxTolerancesScale { length = 1, speed = 10 };
			physics = phys_PxCreatePhysics(versionNumber, foundation, &tolerancesScale, true, pvd, null);

			phys_PxInitExtensions(physics, pvd);

			var sceneDescription = PxSceneDesc_new(PxPhysics_getTolerancesScale(physics));
			sceneDescription.gravity = new PxVec3() { x = 0.0f, y = Gravity, z = 0.0f };
		
			dispatcher = phys_PxDefaultCpuDispatcherCreate(1, null, PxDefaultCpuDispatcherWaitForWorkMode.WaitForWork, 0);
			sceneDescription.cpuDispatcher = (PxCpuDispatcher*)dispatcher;
			sceneDescription.filterShader = get_default_simulation_filter_shader();

			scene = physics->CreateSceneMut(&sceneDescription);
			material = physics->CreateMaterialMut(0.5f, 0.5f, 0.5f);

			// pvd client
			var pvdClient = scene->GetScenePvdClientMut();
			if (pvdClient != null)
			{
				pvdClient->SetScenePvdFlagMut(PxPvdSceneFlag.TransmitConstraints, true);
				pvdClient->SetScenePvdFlagMut(PxPvdSceneFlag.TransmitContacts, true);
				pvdClient->SetScenePvdFlagMut(PxPvdSceneFlag.TransmitScenequeries, true);
			}

			Run();
		}

		private async void Run()
		{
			await Task.Run(() =>
			{
				while (running)
				{
					if (ProjectAbstraction.LoadedProject != null && ProjectAbstraction.LoadedProject.Runtime)
						FixedUpdate();
				}
			});

			Clear();
		}

		private float currentTime = 0;
		private int fps = 0;

		private unsafe void FixedUpdate()
		{
			RemoveActors();
			AddActors();
			SwipeActors();
			ChangeKinematic();
			ApplyTransform();
			ApplyAddForce();
			ApplyVelocity();
			ApplyAngularVelocity();

			foreach (var actor in Actors)
			{
				if (actor.Key == null || actor.Value == null)
					continue;

				PxTransform transform = PxRigidActor_getGlobalPose((PxRigidActor*)actor.Value.Actor);
				actor.Value.Transform = transform;

				if (actor.Value.Dynamic)
				{
					Console.WriteLine("Read: " + actor.Value.Velocity);
					actor.Value.Velocity = PxRigidBody_getLinearVelocity((PxRigidBody*)actor.Value.Actor);
					actor.Value.AngularVelocity = PxRigidBody_getAngularVelocity((PxRigidBody*)actor.Value.Actor);
				}
			}

			if (ProjectAbstraction.LoadedProject != null)
			{
				float resultSpeed = 1f / FixedFrames * ProjectAbstraction.LoadedProject.Time.Speed;
				scene->SimulateMut(1f / FixedFrames, null, null, 0, true);
				//scene->CollideMut(ProjectAbstraction.LoadedProject.Time.RawDeltaTime, null, null, 0, true);
				//scene->FetchCollisionMut(true);
				//scene->AdvanceMut(null);
				uint error = 0;
				scene->FetchResultsMut(true, &error);

				ProjectAbstraction? project = ProjectAbstraction.LoadedProject;

				if (project != null && project.LoadedScene != null)
				{
					if (project.Runtime)
					{
						project.LoadedScene.CallEvent(EventID.FixedUpdate);
					}

					project.LoadedScene.CallEvent(EventID.EditorFixedUpdate);
				}

				currentTime += 1f / FixedFrames;
				fps += 1;
				
				if (currentTime > 1)
				{
					Console.WriteLine(fps);
					fps = 0;
					currentTime = 0;
				}

				Thread.Sleep((int)(1f / FixedFrames * 1000));
			}
		}

		private unsafe void RemoveActors()
		{
			for (int i = 0; i < _removeActors.Count; i++)
			{
				scene->RemoveActorMut(_removeActors[i].Actor, true);
			}
			_removeActors.Clear();
		}

		private unsafe void AddActors()
		{
			for (int i = 0; i < _addActors.Count; i++)
			{
				scene->AddActorMut(_addActors[i].Actor, null);
			}
			_addActors.Clear();
		}

		private unsafe void SwipeActors()
		{
			for (int i = 0; i < _swipeActors.Count; i++)
			{
				PhysicsEntity entity = _swipeActors[i];

				scene->RemoveActorMut(entity.Actor, true);

				PxActor* actor;
				PxTransform transform = entity.Transform;
				if (entity.Dynamic)
				{
					actor = (PxActor*)physics->PhysPxCreateDynamic1(&transform, entity.Shape, 0.5f);
				}
				else
				{
					actor = (PxActor*)physics->PhysPxCreateStatic1(&transform, entity.Shape);
				}

				entity.Actor = actor;
				scene->AddActorMut(actor, null);
			}
			_swipeActors.Clear();
		}

		private unsafe void ChangeKinematic()
		{
			for (int i = 0; i < _changeKinematic.Count; i++)
			{
				PhysicsEntity entity = _changeKinematic[i];

				PxRigidBody_setRigidBodyFlag_mut((PxRigidBody*)entity.Actor, PxRigidBodyFlag.Kinematic, entity.Kinematic);
				if (entity.Kinematic == false)
				{
					// To remove sleeping state we need to apply global pose again
					PxTransform transform = entity.Transform;
					PxRigidActor_setGlobalPose_mut((PxRigidActor*)entity.Actor, &transform, true);
				}
			}
			_changeKinematic.Clear();
		}

		private unsafe void ApplyTransform()
		{
			for (int i = 0; i < _applyTransform.Count; i++)
			{
				PhysicsEntity entity = _applyTransform[i];
				PxTransform transform = entity.Transform;
				PxRigidActor_setGlobalPose_mut((PxRigidActor*)entity.Actor, &transform, true);
			}
			_applyTransform.Clear();
		}

		private unsafe void ApplyAddForce()
		{
			for (int i = 0; i < _applyForce.Count; i++)
			{
				PhysicsEntity entity = _applyForce[i].Entity;
				PxVec3 force = _applyForce[i].Force;
				((PxRigidBody*)entity.Actor)->AddForceMut(&force, _applyForce[i].Mode, true);
			}
			_applyForce.Clear();
		}

		private unsafe void ApplyAddTorque()
		{
			for (int i = 0; i < _applyTorque.Count; i++)
			{
				PhysicsEntity entity = _applyTorque[i].Entity;
				PxVec3 torque = _applyTorque[i].Force;
				((PxRigidBody*)entity.Actor)->AddTorqueMut(&torque, _applyTorque[i].Mode, true);
			}
			_applyTorque.Clear();
		}

		private unsafe void ApplyVelocity()
		{
			for (int i = 0; i < _applyVelocity.Count; i++)
			{
				PhysicsEntity entity = _applyVelocity[i].Entity;
				PxVec3 velocity = _applyVelocity[i].Velocity;
				((PxRigidDynamic*)entity.Actor)->SetLinearVelocityMut(&velocity, true);
			}
			_applyVelocity.Clear();
		}

		private unsafe void ApplyAngularVelocity()
		{
			for (int i = 0; i < _applyAngularVelocity.Count; i++)
			{
				PhysicsEntity entity = _applyAngularVelocity[i].Entity;
				PxVec3 velocity = _applyVelocity[i].Velocity;
				((PxRigidDynamic*)entity.Actor)->SetAngularVelocityMut(&velocity, false);
			}
			_applyAngularVelocity.Clear();
		}

		private unsafe void Clear()
		{
			// Clear existing objects

			PxScene_release_mut(scene);
			PxDefaultCpuDispatcher_release_mut(dispatcher);
			PxPhysics_release_mut(physics);
		}

		public void ChangeDynamic(string physicsID, bool dynamic)
		{
			if (Actors.TryGetValue(physicsID, out PhysicsEntity? actor) == false)
				return;

			actor.Dynamic = dynamic;
			_swipeActors.Add(actor);
		}

		public void ChangeKinematic(string physicsID, bool kinematic)
		{
			if (Actors.TryGetValue(physicsID, out PhysicsEntity? actor) == false)
				return;

			actor.Kinematic = kinematic;
			_changeKinematic.Add(actor);
		}

		public void ApplyTransform(string physicsID, Vector3 position, Quaternion rotation)
		{
			if (Actors.TryGetValue(physicsID, out PhysicsEntity? actor) == false)
				return;

			actor.Transform.p = position;
			actor.Transform.q = rotation;
			_applyTransform.Add(actor);
		}

		public void ApplyAddForce(string physicsID, Vector3 force, PxForceMode mode)
		{
			if (Actors.TryGetValue(physicsID, out PhysicsEntity? actor) == false)
				return;

			_applyForce.Add(new ChangeActorForce() { Entity = actor, Force = force, Mode = mode });
		}

		public void ApplyAddTorque(string physicsID, Vector3 torque, PxForceMode mode)
		{
			if (Actors.TryGetValue(physicsID, out PhysicsEntity? actor) == false)
				return;

			_applyTorque.Add(new ChangeActorForce() { Entity = actor, Force = torque, Mode = mode });
		}

		public void ApplyVelocity(string physicsID, Vector3 velocity)
		{
			if (Actors.TryGetValue(physicsID, out PhysicsEntity? actor) == false)
				return;

			_applyVelocity.Add(new ChangeActorVelocity() { Entity = actor, Velocity = velocity });
		}

		public void ApplyAngularVelocity(string physicsID, Vector3 velocity)
		{
			if (Actors.TryGetValue(physicsID, out PhysicsEntity? actor) == false)
				return;

			_applyAngularVelocity.Add(new ChangeActorVelocity() { Entity = actor, Velocity = velocity });
		}

		public unsafe string CreateStaticCube(Vector3 position, Quaternion rotation, Vector3 scale)
		{
			var geometry = PxBoxGeometry_new_1(scale);
			return CreateStaticObject(position, rotation, (PxGeometry*)&geometry, ColliderType.Cube);
		}

		public unsafe string CreateStaticSphere(Vector3 position, Quaternion rotation, float radius)
		{
			var geometry = PxSphereGeometry_new(radius);
			return CreateStaticObject(position, rotation, (PxGeometry*)&geometry, ColliderType.Sphere);
		}

		public unsafe string CreateStaticPlane(Vector3 position, Quaternion rotation, Vector2 size)
		{
			var geometry = PxPlaneGeometry_new();
			return CreateStaticObject(position, rotation, (PxGeometry*)&geometry, ColliderType.Plane);
		}

		public unsafe string CreateStaticCapsule(Vector3 position, Quaternion rotation, float halfHeight, float radius)
		{
			var geometry = PxCapsuleGeometry_new(radius, halfHeight);
			return CreateStaticObject(position, rotation, (PxGeometry*)&geometry, ColliderType.Capsule);
		}

		public unsafe string CreateStaticObject(Vector3 position, Quaternion rotation, PxGeometry* geometry, ColliderType type)
		{
			var transform = CreateTransform(position, rotation);
			var shape = CreateShape(geometry, material);

			if (type == ColliderType.Capsule)
			{
				PxTransform local = new PxTransform();
				local.p = Vector3.Zero;

				PxVec3 xDir = new PxVec3() { x = 1.0f, y = 0.0f, z = 0.0f };
				PxVec3 upDir = new PxVec3() { x = 0.0f, y = 1.0f, z = 0.0f };	
				local.q = phys_PxShortestRotation(&xDir, &upDir);
				PxShape_setLocalPose_mut(shape, &local);
			}

			PxRigidStatic* staticResult = physics->PhysPxCreateStatic1(&transform, shape);

			return AttachActor(new PhysicsEntity()
			{
				Actor = (PxActor*)staticResult,
				Shape = shape,
				ColliderType = type,
				Material = material,
				Dynamic = false,
				Kinematic = false,
				Transform = transform
			});
		}

		public unsafe void UpdateActorSize(string physicsID, object[] data)
		{
			if (Actors.ContainsKey(physicsID) == false)
				return;

			PhysicsEntity entity = Actors[physicsID];

			PxShape* shape = entity.Shape;
			PxGeometryHolder* holder = (PxGeometryHolder*)shape->GetGeometry();

			if (data.Length >= 2)
			{
				switch (entity.ColliderType)
				{
					case ColliderType.Plane:
						Vector3 planeSize = holder->TriangleMesh()->scale.scale;
						planeSize.X *= ((Vector2)(BEngine.Vector2)data[1]).Y;
						planeSize.Z *= ((Vector2)(BEngine.Vector2)data[1]).X;
						holder->TriangleMesh()->scale.scale = planeSize;
						break;
					case ColliderType.Cube:
						holder->Box()->halfExtents = (PxVec3)(Vector3)(BEngine.Vector3)data[1];
						break;
					case ColliderType.Sphere:
						holder->Sphere()->radius = (float)data[1];
						break;
					case ColliderType.Capsule:
						holder->Capsule()->halfHeight = (float)data[1];
						holder->Capsule()->radius = (float)data[2];
						break;
				}
			}

			Vector3 current = holder->TriangleMesh()->scale.scale;
			current.X *= ((Vector3)(BEngine.Vector3)data[0]).X;
			current.Y *= ((Vector3)(BEngine.Vector3)data[0]).Y;
			current.Z *= ((Vector3)(BEngine.Vector3)data[0]).Z;
			holder->TriangleMesh()->scale.scale = current;

			entity.Shape->SetGeometryMut(holder->Any());
		}

		private unsafe PxTransform CreateTransform(PxVec3 position, PxQuat rotation)
		{
			return PxTransform_new_5(&position, &rotation);
		}

		private unsafe PxShape* CreateShape(PxGeometry* geometry, PxMaterial* material)
		{
			return physics->CreateShapeMut(geometry, material, true, PxShapeFlags.SimulationShape);
		}

		private unsafe string AttachActor(PhysicsEntity physicsEntity)
		{
			string id = Guid.NewGuid().ToString();

			_addActors.Add(physicsEntity);
			Actors.TryAdd(id, physicsEntity);

			return id;
		}

		public unsafe PhysicsEntryData GetActorData(string physicsID)
		{
			if (Actors.ContainsKey(physicsID) == false)
				return new PhysicsEntryData();

			PxTransform transform = Actors[physicsID].Transform;

			return new PhysicsEntryData() { Position = (Vector3)transform.p, Rotation = (Quaternion)transform.q };
		}

		public unsafe Vector3 GetVelocity(string physicsID)
		{
			if (Actors.ContainsKey(physicsID) == false)
				return Vector3.Zero;

			Console.WriteLine("Get: " + Actors[physicsID].Velocity);
			return Actors[physicsID].Velocity;
		}

		public unsafe Vector3 GetAngularVelocity(string physicsID)
		{
			if (Actors.ContainsKey(physicsID) == false)
				return Vector3.Zero;

			return Actors[physicsID].AngularVelocity;
		}

		public unsafe void RemoveActor(string physicsID)
		{
			if (Actors.ContainsKey(physicsID) == false)
				return;

			_removeActors.Add(Actors[physicsID]);
			Actors.TryRemove(physicsID, out PhysicsEntity? old);
		}
	}
}
