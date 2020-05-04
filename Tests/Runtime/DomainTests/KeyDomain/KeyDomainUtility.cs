using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;

namespace KeyDomain
{
    static class KeyDomainUtility
    {
        public static TraitBasedObject Agent;
        public static TraitBasedObject BlackKey;
        public static TraitBasedObject WhiteKey;
        public static TraitBasedObject StartRoom;
        public static TraitBasedObject FirstRoom;

        public static ObjectId AgentId;
        public static ObjectId BlackKeyId;
        public static ObjectId WhiteKeyId;
        public static ObjectId StartRoomId;
        public static ObjectId FirstRoomId;

        public static ComponentType[] RoomArchetype;

        public static StateEntityKey InitialStateKey;
        public static StateData InitialState => StateManager.GetStateData(InitialStateKey);
        public static StateManager StateManager;

        public static void Initialize(World world)
        {
            RoomArchetype = new ComponentType[]{ ComponentType.ReadWrite<Lockable>(), ComponentType.ReadWrite<Colored>(), ComponentType.ReadWrite<TraitBasedObjectId>() };

            StateManager = world.GetOrCreateSystem<StateManager>();
            var stateData = StateManager.CreateStateData();

            (BlackKey, BlackKeyId) = CreateKey(stateData, ColorValue.Black);
            (WhiteKey, WhiteKeyId) = CreateKey(stateData, ColorValue.White);
            (StartRoom, StartRoomId) = CreateRoom(stateData, ColorValue.Black, false);
            (FirstRoom, FirstRoomId) = CreateRoom(stateData, ColorValue.White);
            (Agent, AgentId) = CreateAgent(stateData, BlackKeyId, StartRoomId);

            InitialStateKey = StateManager.GetStateDataKey(stateData);
        }

        public static (TraitBasedObject, ObjectId) CreateRoom(StateData testState, ColorValue color, bool locked = true)
        {
            using (var roomType =  new NativeArray<ComponentType>(2, Allocator.TempJob) { [0] = ComponentType.ReadWrite<Lockable>(),  [1] = ComponentType.ReadWrite<Colored>() })
            {
                testState.AddObject(roomType, out var room, out var roomId);

                var lockables = testState.LockableBuffer;
                var coloreds = testState.ColoredBuffer;

                lockables[room.LockableIndex] = new Lockable {Locked = locked};
                coloreds[room.ColoredIndex] = new Colored {Color = color};

                return (room, roomId.Id);
            }
        }

        public static (TraitBasedObject, ObjectId) CreateKey(StateData testState, ColorValue color)
        {
            using (var keyType = new NativeArray<ComponentType>(2, Allocator.TempJob) { [0] = ComponentType.ReadWrite<Carriable>(), [1] = ComponentType.ReadWrite<Colored>() })
            {
                testState.AddObject(keyType, out var key, out var keyId);

                var carriables = testState.CarriableBuffer;
                carriables[key.CarriableIndex] = new Carriable {Carrier = ObjectId.None};

                var coloreds = testState.ColoredBuffer;
                coloreds[key.ColoredIndex] = new Colored {Color = color};

                return (key, keyId.Id);
            }
        }

        public static (TraitBasedObject, ObjectId) CreateAgent(StateData testState, ObjectId keyId, ObjectId roomId)
        {
            using (var agentType = new NativeArray<ComponentType>(2, Allocator.TempJob) { [0] = ComponentType.ReadWrite<Carrier>(), [1] = ComponentType.ReadWrite<Localized>() })
            {
                testState.AddObject(agentType, out var agent, out var agentId);
                var carriers = testState.CarrierBuffer;
                var localizeds = testState.LocalizedBuffer;

                var traitBasedObjects = testState.TraitBasedObjects;

                carriers[agent.CarrierIndex] = new Carrier {CarriedObject = keyId};
                localizeds[agent.LocalizedIndex] = new Localized {Location = roomId};

                var carriables = testState.CarriableBuffer;

                var objectIds = testState.TraitBasedObjectIds;
                for (int i = 0; i < objectIds.Length; i++)
                {
                    if (objectIds[i].Id == keyId)
                    {
                        carriables[traitBasedObjects[i].CarriableIndex] = new Carriable {Carrier = agentId.Id};
                    }
                }

                return (agent, agentId.Id);
            }
        }
    }
}
