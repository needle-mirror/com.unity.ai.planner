using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Entities;

namespace KeyDomain
{
    static class KeyDomainUtility
    {
        public static DomainObject Agent;
        public static DomainObject BlackKey;
        public static DomainObject WhiteKey;
        public static DomainObject StartRoom;
        public static DomainObject FirstRoom;

        public static ObjectID AgentID;
        public static ObjectID BlackKeyID;
        public static ObjectID WhiteKeyID;
        public static ObjectID StartRoomID;
        public static ObjectID FirstRoomID;

        public static ComponentType[] RoomArchetype;

        public static StateEntityKey InitialStateKey;
        public static StateData InitialState => s_StateManager.GetStateData(InitialStateKey);
        static StateManager s_StateManager;

        public static void Initialize(World world)
        {
            s_StateManager = world.GetOrCreateSystem<StateManager>();

            RoomArchetype = new ComponentType[]{ typeof(Lockable), typeof(Colored), typeof(DomainObjectID) };

            var stateData = s_StateManager.CreateStateData();

            (StartRoom, StartRoomID) = CreateRoom(stateData, ColorValue.Black, false);
            (FirstRoom, FirstRoomID) = CreateRoom(stateData, ColorValue.White);
            (BlackKey, BlackKeyID) = CreateKey(stateData, ColorValue.Black);
            (WhiteKey, WhiteKeyID) = CreateKey(stateData, ColorValue.White);
            (Agent, AgentID) = CreateAgent(stateData, BlackKeyID, StartRoomID);

            InitialStateKey = s_StateManager.GetStateDataKey(stateData);
        }

        public static (DomainObject, ObjectID) CreateRoom(StateData testState, ColorValue color, bool locked = true)
        {
            var (room, roomID) = testState.AddDomainObject(new ComponentType[] { typeof(Lockable), typeof(Colored) });
            var lockables = testState.LockableBuffer;
            var coloreds = testState.ColoredBuffer;

            lockables[room.LockableIndex] = new Lockable { Locked = locked };
            coloreds[room.ColoredIndex] = new Colored{ Color = color };

            return (room, roomID.ID);
        }

        public static (DomainObject, ObjectID) CreateKey(StateData testState, ColorValue color)
        {
            var (key, keyID) = testState.AddDomainObject(new ComponentType[] { typeof(Carriable), typeof(Colored) });

            var carriables = testState.CarriableBuffer;
            carriables[key.CarriableIndex] = new Carriable { Carrier = ObjectID.None };

            var coloreds = testState.ColoredBuffer;
            coloreds[key.ColoredIndex] = new Colored { Color = color };

            return (key, keyID.ID);
        }

        public static (DomainObject, ObjectID) CreateAgent(StateData testState, ObjectID keyID, ObjectID roomID)
        {
            var (agent, agentID) = testState.AddDomainObject(new ComponentType[] { typeof(Carrier), typeof(Localized) });
            var carriers = testState.CarrierBuffer;
            var localizeds = testState.LocalizedBuffer;

            var domainObjects = testState.DomainObjects;

            carriers[agent.CarrierIndex] = new Carrier { CarriedObject = keyID };
            localizeds[agent.LocalizedIndex] = new Localized { Location =  roomID};

            var carriables = testState.CarriableBuffer;

            var objectIDs = testState.DomainObjectIDs;
            for (int i = 0; i < objectIDs.Length; i++)
            {
                if (objectIDs[i].ID == keyID)
                {
                    carriables[domainObjects[i].CarriableIndex] = new Carriable {Carrier = agentID.ID};
                }
            }

            return (agent, agentID.ID);
        }
    }
}
