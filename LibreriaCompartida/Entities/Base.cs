using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace LibreriaCompartida.Entities {
	public abstract class Base {
		public abstract string PK { get; }
		public abstract string SK { get; }

		public Dictionary<string, AttributeValue> Key {
			get {
				return new Dictionary<string, AttributeValue> {
					{ "PK", new AttributeValue() { S = PK } },
					{ "SK", new AttributeValue() { S = SK } }
				};
			}
		}

		public abstract Dictionary<string, AttributeValue> ToItem();
	}
}
