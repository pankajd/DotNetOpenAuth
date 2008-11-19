﻿//-----------------------------------------------------------------------
// <copyright file="OpenIdChannelTests.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.Test.OpenId.ChannelElements {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Text;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.Messaging.Reflection;
	using DotNetOpenAuth.OpenId.ChannelElements;
	using Microsoft.VisualStudio.TestTools.UnitTesting;

	[TestClass]
	public class OpenIdChannelTests : TestBase {
		private OpenIdChannel channel;
		private OpenIdChannel_Accessor accessor;
		private Mocks.TestWebRequestHandler webHandler;

		[TestInitialize]
		public void Setup() {
			this.webHandler = new Mocks.TestWebRequestHandler();
			this.channel = new OpenIdChannel();
			this.accessor = OpenIdChannel_Accessor.AttachShadow(this.channel);
			this.channel.WebRequestHandler = this.webHandler;
		}

		[TestMethod]
		public void Ctor() {
		}

		/// <summary>
		/// Verifies that the channel sends direct message requests as HTTP POST requests.
		/// </summary>
		[TestMethod]
		public void DirectRequestsUsePost() {
			IDirectedProtocolMessage requestMessage = new Mocks.TestDirectedMessage(MessageTransport.Direct) {
				Recipient = new Uri("http://host"),
				Name = "Andrew",
			};
			HttpWebRequest httpRequest = this.accessor.CreateHttpRequest(requestMessage);
			Assert.AreEqual("POST", httpRequest.Method);
			StringAssert.Contains(this.webHandler.RequestEntityAsString, "Name=Andrew");
		}

		/// <summary>
		/// Verifies that direct response messages are encoded using Key Value Form.
		/// </summary>
		/// <remarks>
		/// The validity of the actual KVF encoding is not checked here.  We assume that the KVF encoding
		/// class is verified elsewhere.  We're only checking that the KVF class is being used by the 
		/// <see cref="OpenIdChannel.SendDirectMessageResponse"/> method.
		/// </remarks>
		[TestMethod]
		public void DirectResponsesSentUsingKeyValueForm() {
			IProtocolMessage message = MessagingTestBase.GetStandardTestMessage(MessagingTestBase.FieldFill.AllRequired);
			MessageDictionary messageFields = new MessageDictionary(message);
			byte[] expectedBytes = new KeyValueFormEncoding().GetBytes(messageFields);
			string expectedContentType = OpenIdChannel_Accessor.KeyValueFormContentType;

			Response directResponse = this.accessor.SendDirectMessageResponse(message);
			Assert.AreEqual(expectedContentType, directResponse.Headers[HttpResponseHeader.ContentType]);
			byte[] actualBytes = new byte[directResponse.ResponseStream.Length];
			directResponse.ResponseStream.Read(actualBytes, 0, actualBytes.Length);
			Assert.IsTrue(MessagingUtilities.AreEquivalent(expectedBytes, actualBytes));
		}

		/// <summary>
		/// Verifies that direct message responses are read in using the Key Value Form decoder.
		/// </summary>
		[TestMethod]
		public void DirectResponsesReceivedAsKeyValueForm() {
			var fields = new Dictionary<string, string> {
				{ "var1", "value1" },
				{ "var2", "value2" },
			};
			KeyValueFormEncoding kvf = new KeyValueFormEncoding();
			Response response = new Response {
				ResponseStream = new MemoryStream(kvf.GetBytes(fields)),
			};
			Assert.IsTrue(MessagingUtilities.AreEquivalent(fields, this.accessor.ReadFromResponseInternal(response)));
		}
	}
}