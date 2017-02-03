﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace EnterpriseWebLibrary.EnterpriseWebFramework {
	/// <summary>
	/// An invisible marker used in conjunction with fragment navigation.
	/// </summary>
	public sealed class FragmentMarker: PhrasingComponent {
		private readonly IReadOnlyCollection<FlowComponentOrNode> children;

		/// <summary>
		/// Creates a fragment marker.
		/// </summary>
		/// <param name="id">The ID of the marker, which should be unique on the page. Do not pass null or the empty string.</param>
		public FragmentMarker( string id ) {
			children =
				new PageElement( context => new ElementData( () => new ElementLocalData( "span", Enumerable.Empty<Tuple<string, string>>(), id, "" ) ) ).ToCollection();
		}

		IEnumerable<FlowComponentOrNode> FlowComponent.GetChildren() {
			return children;
		}
	}
}