using System;
using System.Text;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    /// <summary>
    /// A concrete action key for trait-based state representation
    /// </summary>
    public struct ActionKey : IEquatable<ActionKey>, IActionKeyWithGuid
    {
        /// <summary>
        /// A GUID (Globally Unique Identifier) for an action type
        /// </summary>
        public Guid ActionGuid { get; set; }

        int m_Argument0;
        int m_Argument1;
        int m_Argument2;
        int m_Argument3;
        int m_Argument4;
        int m_Argument5;
        int m_Argument6;
        int m_Argument7;
        int m_Argument8;
        int m_Argument9;
        int m_Argument10;
        int m_Argument11;
        int m_Argument12;
        int m_Argument13;
        int m_Argument14;
        int m_Argument15;

        /// <summary>
        /// The number of arguments for this action
        /// </summary>
        public int Length => m_Length;

        /// <summary>
        /// The maximum number of arguments that an action can have
        /// </summary>
        public static int MaxLength => 16;

        /// <summary>
        /// Access an action argument by index
        /// </summary>
        /// <param name="index">Index of action argument</param>
        /// <exception cref="IndexOutOfRangeException">Throws an exception if the index is >= Length</exception>
        public int this[int index]
        {
            get
            {
                if (index >= Length)
                    throw new IndexOutOfRangeException();

                switch (index)
                {
                    case 0:
                        return m_Argument0;
                    case 1:
                        return m_Argument1;
                    case 2:
                        return m_Argument2;
                    case 3:
                        return m_Argument3;
                    case 4:
                        return m_Argument4;
                    case 5:
                        return m_Argument5;
                    case 6:
                        return m_Argument6;
                    case 7:
                        return m_Argument7;
                    case 8:
                        return m_Argument8;
                    case 9:
                        return m_Argument9;
                    case 10:
                        return m_Argument10;
                    case 11:
                        return m_Argument11;
                    case 12:
                        return m_Argument12;
                    case 13:
                        return m_Argument13;
                    case 14:
                        return m_Argument14;
                    case 15:
                        return m_Argument15;

                    default:
                        throw new IndexOutOfRangeException();
                }
            }
            set
            {
                if (index >= Length)
                    throw new IndexOutOfRangeException();

                switch (index)
                {
                    case 0:
                        m_Argument0 = value;
                        break;
                    case 1:
                        m_Argument1 = value;
                        break;
                    case 2:
                        m_Argument2 = value;
                        break;
                    case 3:
                        m_Argument3 = value;
                        break;
                    case 4:
                        m_Argument4 = value;
                        break;
                    case 5:
                        m_Argument5 = value;
                        break;
                    case 6:
                        m_Argument6 = value;
                        break;
                    case 7:
                        m_Argument7 = value;
                        break;
                    case 8:
                        m_Argument8 = value;
                        break;
                    case 9:
                        m_Argument9 = value;
                        break;
                    case 10:
                        m_Argument10 = value;
                        break;
                    case 11:
                        m_Argument11 = value;
                        break;
                    case 12:
                        m_Argument12 = value;
                        break;
                    case 13:
                        m_Argument13 = value;
                        break;
                    case 14:
                        m_Argument14 = value;
                        break;
                    case 15:
                        m_Argument15 = value;
                        break;

                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }

        int m_Length;

        /// <summary>
        /// Create a new action key with a specified number of arguments
        /// </summary>
        /// <param name="length"></param>
        public ActionKey(int length)
        {
            m_Length = length;
            m_Argument0 = m_Argument1 = m_Argument2 = m_Argument3 = m_Argument4 = m_Argument5 = m_Argument6 = m_Argument7 =
                m_Argument8 = m_Argument9 = m_Argument10 = m_Argument11 = m_Argument12 = m_Argument13 = m_Argument14 = m_Argument15 = -1;
        }

        /// <summary>
        /// Test for equality
        /// </summary>
        /// <param name="other">Other action</param>
        /// <returns>Result of equality test</returns>
        public bool Equals(ActionKey other)
        {
            return ActionGuid.Equals(other.ActionGuid)
                && m_Argument0 == other.m_Argument0
                && m_Argument1 == other.m_Argument1
                && m_Argument2 == other.m_Argument2
                && m_Argument3 == other.m_Argument3
                && m_Argument4 == other.m_Argument4
                && m_Argument5 == other.m_Argument5
                && m_Argument6 == other.m_Argument6
                && m_Argument7 == other.m_Argument7
                && m_Argument8 == other.m_Argument8
                && m_Argument9 == other.m_Argument9
                && m_Argument10 == other.m_Argument10
                && m_Argument11 == other.m_Argument11
                && m_Argument12 == other.m_Argument12
                && m_Argument13 == other.m_Argument13
                && m_Argument14 == other.m_Argument14
                && m_Argument15 == other.m_Argument15
                && m_Length == other.m_Length;
        }

        /// <summary>
        /// Test for equality
        /// </summary>
        /// <param name="obj">Other action</param>
        /// <returns>Result of equality test</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
                return Equals(default);

            return obj is ActionKey other && Equals(other);
        }

        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ActionGuid.GetHashCode();
                hashCode = (hashCode * 397) ^ m_Argument0;
                hashCode = (hashCode * 397) ^ m_Argument1;
                hashCode = (hashCode * 397) ^ m_Argument2;
                hashCode = (hashCode * 397) ^ m_Argument3;
                hashCode = (hashCode * 397) ^ m_Argument4;
                hashCode = (hashCode * 397) ^ m_Argument5;
                hashCode = (hashCode * 397) ^ m_Argument6;
                hashCode = (hashCode * 397) ^ m_Argument7;
                hashCode = (hashCode * 397) ^ m_Argument8;
                hashCode = (hashCode * 397) ^ m_Argument9;
                hashCode = (hashCode * 397) ^ m_Argument10;
                hashCode = (hashCode * 397) ^ m_Argument11;
                hashCode = (hashCode * 397) ^ m_Argument12;
                hashCode = (hashCode * 397) ^ m_Argument13;
                hashCode = (hashCode * 397) ^ m_Argument14;
                hashCode = (hashCode * 397) ^ m_Argument15;
                hashCode = (hashCode * 397) ^ m_Length;
                return hashCode;
            }
        }

        /// <summary>
        /// Returns a string that represents the ActionKey
        /// </summary>
        /// <returns>A string that represents the ActionKey</returns>
        public override string ToString()
        {
            var sb = new StringBuilder("ActionKey(");
            sb.Append($"{ActionGuid.ToString()},");
            for (int i = 0; i < Length; i++)
                sb.Append($" {this[i]},");
            sb.Append(")");
            return sb.ToString();
        }
    }
}
