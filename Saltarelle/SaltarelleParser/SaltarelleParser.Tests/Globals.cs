﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;
using Rhino.Mocks;
using Saltarelle;

namespace SaltarelleParser.Tests {
	internal static class Globals {
		internal static XmlNode GetXmlNode(string content) {
			var doc = new XmlDocument() { PreserveWhitespace = true };
			doc.LoadXml("<root>" + content + "</root>");
			return doc.DocumentElement.ChildNodes[0];
		}

		internal static void AssertThrows<T>(Action a, Func<T, bool> exceptionChecker) where T : Exception {
			try {
				a();
			}
			catch (T ex) {
				Assert.IsTrue(exceptionChecker(ex));
				return;
			}
			catch (Exception ex) {
				if (ex is AssertionException)
					throw;
				Assert.Fail("Bad exception " + ex + " thrown.");
				return; // strictly unnecessary
			}
			Assert.Fail("No exception was thrown");
		}
	}
}
