using System;
using System.Collections.Generic;
using System.Text;

namespace PSSApplication.Core
{
    public class GattService
    {
        /// <summary>
        /// The human-readable name for the service
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The uniform identifier that is unique to this service
        /// </summary>
        public string UniformTypeIdentifier { get; }

        /// <summary>
        /// The 16-bit assigned number for this service.
        /// The Bluetooth GATT Service UUID contains this.
        /// </summary>
        public ushort AssignedNumber { get; }

        /// <summary>
        /// The type of specification that this service is.
        /// <seealso cref="https://www.bluetooth.com/specifications/gatt/"/>
        /// </summary>
        public string ProfileSpecification { get; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public GattService(string name, string uniformIdenfier, ushort assignedNumber, string profileSpecification)
        {
            Name = name;
            UniformTypeIdentifier = uniformIdenfier;
            AssignedNumber = assignedNumber;
            ProfileSpecification = profileSpecification;
        }
    }
}
