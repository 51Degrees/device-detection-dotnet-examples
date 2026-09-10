/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Example.Tests.Shared
{
    /// <summary>
    /// Guards against test classes that the MSTest adapter cannot discover.
    /// <para>
    /// When a class carries a fixture attribute on a method with the wrong
    /// signature, for example a <c>[ClassInitialize]</c> method that is not
    /// static, MSTest writes a discovery message to the console and then
    /// drops the whole class. Every test in that class stops running, the
    /// exit code stays zero and the run still reports "Passed!". Nine
    /// Selenium classes in this repository were dropped that way and nobody
    /// noticed, because a warning that nothing reads is the same as no
    /// warning at all.
    /// </para>
    /// <para>
    /// This file is linked into every test project, see the
    /// <c>Tests\Shared</c> Compile item in each .csproj, so that each test
    /// assembly checks itself. The rules the adapter applies are checked
    /// here by reflection and a breach fails as a normal test, which turns
    /// a silent drop into a red run.
    /// </para>
    /// <para>
    /// Every type in the assembly is checked, not only the ones marked
    /// <c>[TestClass]</c>, because base classes carry fixture methods too
    /// and a base class with a bad fixture drops every class beneath it.
    /// </para>
    /// </summary>
    [TestClass]
    public class TestDiscoveryGuard
    {
        /// <summary>
        /// Binding flags covering everything the adapter looks at. Non
        /// public methods are included so that a fixture method someone
        /// forgot to make public is still reported.
        /// </summary>
        private const BindingFlags MethodFlags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        /// <summary>
        /// A TestContext parameter is required by the attribute.
        /// </summary>
        private const int Required = 1;

        /// <summary>
        /// A TestContext parameter is allowed but not required.
        /// </summary>
        private const int Optional = 0;

        /// <summary>
        /// The method must take no parameters at all.
        /// </summary>
        private const int Forbidden = -1;

        /// <summary>
        /// The assembly this copy of the guard was compiled into. The
        /// guard's own type is used rather than
        /// <see cref="Assembly.GetExecutingAssembly"/> so that the source
        /// file can be linked into several projects and each copy still
        /// checks the assembly it belongs to.
        /// </summary>
        private static Assembly AssemblyUnderTest =>
            typeof(TestDiscoveryGuard).Assembly;

        /// <summary>
        /// Fails when any type in this assembly declares a fixture or test
        /// method that MSTest cannot use. The message names every offending
        /// method and says what is wrong with it.
        /// </summary>
        [TestMethod]
        public void AllTestClassesCanBeDiscovered()
        {
            var problems = new List<string>();

            foreach (var type in GetLoadableTypes(AssemblyUnderTest))
            {
                foreach (var method in type.GetMethods(MethodFlags))
                {
                    CheckMethod(type, method, problems);
                }
            }

            if (problems.Count > 0)
            {
                var message = new StringBuilder();
                message.AppendLine(
                    $"{problems.Count} method(s) in " +
                    $"'{AssemblyUnderTest.GetName().Name}' carry an MSTest " +
                    "attribute with a signature the adapter cannot use. " +
                    "MSTest drops the whole class at discovery and the run " +
                    "still reports success, so every test in these classes " +
                    "would never run:");
                foreach (var problem in problems.OrderBy(p => p))
                {
                    message.AppendLine("  " + problem);
                }
                Assert.Fail(message.ToString());
            }
        }

        /// <summary>
        /// Checks one method against the rules for whichever MSTest
        /// attribute it carries, adding a description of anything wrong to
        /// <paramref name="problems"/>.
        /// </summary>
        private static void CheckMethod(
            Type type,
            MethodInfo method,
            List<string> problems)
        {
            if (HasAttribute<AssemblyInitializeAttribute>(method))
            {
                CheckFixture(
                    type, method, problems, "AssemblyInitialize",
                    mustBeStatic: true, testContextParameter: Required);
            }
            if (HasAttribute<AssemblyCleanupAttribute>(method))
            {
                CheckFixture(
                    type, method, problems, "AssemblyCleanup",
                    mustBeStatic: true, testContextParameter: Optional);
            }
            if (HasAttribute<ClassInitializeAttribute>(method))
            {
                CheckFixture(
                    type, method, problems, "ClassInitialize",
                    mustBeStatic: true, testContextParameter: Required);
            }
            if (HasAttribute<ClassCleanupAttribute>(method))
            {
                CheckFixture(
                    type, method, problems, "ClassCleanup",
                    mustBeStatic: true, testContextParameter: Optional);
            }
            if (HasAttribute<TestInitializeAttribute>(method))
            {
                CheckFixture(
                    type, method, problems, "TestInitialize",
                    mustBeStatic: false, testContextParameter: Forbidden);
            }
            if (HasAttribute<TestCleanupAttribute>(method))
            {
                CheckFixture(
                    type, method, problems, "TestCleanup",
                    mustBeStatic: false, testContextParameter: Forbidden);
            }
            if (HasAttribute<TestMethodAttribute>(method))
            {
                CheckTestMethod(type, method, problems);
            }
        }

        /// <summary>
        /// Applies the shared rules for a fixture method, which are that it
        /// must be public, must be static or an instance method as the
        /// attribute demands, must return void or an awaitable, and must
        /// take the <see cref="TestContext"/> parameter the attribute
        /// demands.
        /// </summary>
        /// <param name="testContextParameter">
        /// One of <see cref="Required"/>, <see cref="Optional"/> or
        /// <see cref="Forbidden"/>.
        /// </param>
        private static void CheckFixture(
            Type type,
            MethodInfo method,
            List<string> problems,
            string attributeName,
            bool mustBeStatic,
            int testContextParameter)
        {
            var name = $"{type.FullName}.{method.Name} [{attributeName}]";

            if (method.IsPublic == false)
            {
                problems.Add($"{name} must be public.");
            }
            if (mustBeStatic == true && method.IsStatic == false)
            {
                problems.Add(
                    $"{name} must be static. MSTest cannot reach an " +
                    "instance method from a class level fixture, so the " +
                    "whole class is dropped at discovery.");
            }
            if (mustBeStatic == false && method.IsStatic == true)
            {
                problems.Add($"{name} must not be static.");
            }
            if (ReturnsVoidOrAwaitable(method) == false)
            {
                problems.Add(
                    $"{name} must return void, Task or ValueTask, but " +
                    $"returns '{method.ReturnType.Name}'.");
            }

            var parameters = method.GetParameters();
            var takesContext =
                parameters.Length == 1 &&
                parameters[0].ParameterType == typeof(TestContext);

            if (testContextParameter == Required && takesContext == false)
            {
                problems.Add(
                    $"{name} must take a single TestContext parameter, but " +
                    $"takes {DescribeParameters(parameters)}.");
            }
            if (testContextParameter == Optional &&
                parameters.Length != 0 &&
                takesContext == false)
            {
                problems.Add(
                    $"{name} must take no parameters or a single " +
                    "TestContext parameter, but takes " +
                    $"{DescribeParameters(parameters)}.");
            }
            if (testContextParameter == Forbidden && parameters.Length != 0)
            {
                problems.Add(
                    $"{name} must take no parameters, but takes " +
                    $"{DescribeParameters(parameters)}.");
            }
        }

        /// <summary>
        /// Applies the rules for a test method, which must be public, must
        /// not be static and must return void or an awaitable. A test
        /// method only takes parameters when the class supplies them with a
        /// data source attribute, so parameters are not checked here.
        /// </summary>
        private static void CheckTestMethod(
            Type type,
            MethodInfo method,
            List<string> problems)
        {
            var name = $"{type.FullName}.{method.Name} [TestMethod]";

            if (method.IsPublic == false)
            {
                problems.Add($"{name} must be public.");
            }
            if (method.IsStatic == true)
            {
                problems.Add($"{name} must not be static.");
            }
            if (ReturnsVoidOrAwaitable(method) == false)
            {
                problems.Add(
                    $"{name} must return void, Task or ValueTask, but " +
                    $"returns '{method.ReturnType.Name}'.");
            }
        }

        /// <summary>
        /// True when the method returns void or something MSTest can await.
        /// </summary>
        private static bool ReturnsVoidOrAwaitable(MethodInfo method)
        {
            var returnType = method.ReturnType;
            return returnType == typeof(void) ||
                returnType == typeof(Task) ||
                returnType == typeof(ValueTask);
        }

        /// <summary>
        /// Describes a parameter list for the failure message.
        /// </summary>
        private static string DescribeParameters(ParameterInfo[] parameters)
        {
            if (parameters.Length == 0)
            {
                return "no parameters";
            }
            return "(" + string.Join(
                ", ", parameters.Select(p => p.ParameterType.Name)) + ")";
        }

        /// <summary>
        /// True when the method carries the given attribute, including one
        /// inherited from a base class method it overrides.
        /// </summary>
        private static bool HasAttribute<T>(MethodInfo method)
            where T : Attribute =>
            method.GetCustomAttribute<T>(inherit: true) != null;

        /// <summary>
        /// Returns the types that loaded. A test assembly can reference a
        /// type it never uses at runtime, and asking for every type would
        /// then throw before any checking happened, so the types that did
        /// load are checked and the rest are ignored.
        /// </summary>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(t => t != null);
            }
        }
    }
}
