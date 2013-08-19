﻿using System;
using System.Linq;
using System.Web;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Controls;
using RedStapler.StandardLibrary.WebSessionState;
using Stripe;

namespace RedStapler.StandardLibrary.EnterpriseWebFramework {
	/// <summary>
	/// Methods for collecting credit-card information with Stripe.
	/// </summary>
	public static class CreditCardCollectionStatics {
		/// <summary>
		/// Returns a JavaScript function call getter that opens a Stripe Checkout modal window. If the window's submit button is clicked, the credit card is
		/// charged or otherwise used. Do not execute the getter before all controls have IDs.
		/// </summary>
		/// <param name="testPublishableKey">Your test publishable API key. Will be used in non-live installations. Do not pass null.</param>
		/// <param name="livePublishableKey">Your live publishable API key. Will be used in live installations. Do not pass null.</param>
		/// <param name="name">See https://stripe.com/docs/checkout. Do not pass null.</param>
		/// <param name="description">See https://stripe.com/docs/checkout. Do not pass null.</param>
		/// <param name="amountInCents">See https://stripe.com/docs/checkout </param>
		/// <param name="testSecretKey">Your test secret API key. Will be used in non-live installations. Do not pass null.</param>
		/// <param name="liveSecretKey">Your live secret API key. Will be used in live installations. Do not pass null.</param>
		/// <param name="successMessage">The status message that will appear if the credit-card submission is successful. Do not pass null.</param>
		/// <param name="successPage">The page to which the user will be redirected if the credit-card submission is successful. Pass null for no redirection.
		/// </param>
		public static Func<string> GetCollectionJsFunctionCall( string testPublishableKey, string livePublishableKey, string name, string description,
		                                                        int? amountInCents, string testSecretKey, string liveSecretKey, string successMessage,
		                                                        PageInfo successPage ) {
			if( !HttpContext.Current.Request.IsSecureConnection )
				throw new ApplicationException( "Credit-card collection can only be done from secure pages." );
			EwfPage.Instance.ClientScript.RegisterClientScriptInclude( typeof( CreditCardCollectionStatics ),
			                                                           "Stripe Checkout",
			                                                           "https://checkout.stripe.com/v2/checkout.js" );

			var dm = new DataModification();
			var token = new DataValue<string>();

			Func<PostBackValueDictionary, string> tokenHiddenFieldValueGetter; // unused
			Func<string> tokenHiddenFieldClientIdGetter;
			EwfHiddenField.Create( "", postBackValue => token.Value = postBackValue, dm, out tokenHiddenFieldValueGetter, out tokenHiddenFieldClientIdGetter );

			var externalHandler =
				new ExternalPostBackEventHandler( () => EwfPage.Instance.ExecuteDataModification( dm, () => EwfPage.Instance.EhRedirect( successPage ) ) );
			EwfPage.Instance.Form.Controls.Add( externalHandler );

			dm.AddModificationMethod( () => {
				// We can add support later for customer creation, subscriptions, etc. as needs arise.
				if( !amountInCents.HasValue )
					throw new ApplicationException( "Only simple charges are supported at this time." );

				var apiKey = AppTools.IsLiveInstallation ? liveSecretKey : testSecretKey;
				dynamic response = new StripeClient( apiKey ).CreateCharge( amountInCents.Value / 100m,
				                                                            "usd",
				                                                            new CreditCardToken( token.Value ),
				                                                            description: description.Any() ? description : null );
				if( response.IsError ) {
					if( response.error.type == "card_error" )
						throw new EwfException( response.error.message );
					throw new ApplicationException( "Stripe error: " + response );
				}

				if( successMessage.Any() )
					EwfPage.AddStatusMessage( StatusMessageType.Info, successMessage );
			} );

			return () => {
				var jsTokenHandler = "function( res ) { $( '#" + tokenHiddenFieldClientIdGetter() + "' ).val( res.id ); " +
				                     PostBackButton.GetPostBackScript( externalHandler, true, includeReturnFalse: false ) + "; }";
				return "StripeCheckout.open( { key: '" + ( AppTools.IsLiveInstallation ? livePublishableKey : testPublishableKey ) + "', name: '" + name +
				       "', description: '" + description + "', " + ( amountInCents.HasValue ? "amount: " + amountInCents.Value + ", " : "" ) + "token: " + jsTokenHandler +
				       " } )";
			};
		}
	}
}