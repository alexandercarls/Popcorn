﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Popcorn.Helpers
{
    /// <summary>
    /// Used to define extension methods
    /// </summary>
    public static class Extensions
    {
        /// <summary>
		/// Finds a parent of a given item on the visual tree.
		/// </summary>
		/// <param name="child">A direct or indirect child of the
		/// queried item.</param>
		/// <param name="elementName">Parent name</param>
		/// <returns>The first parent item that matches the submitted
		/// type parameter. If not matching item can be found, a null
		/// reference is being returned.</returns>
		public static FrameworkElement FindParentElement(this FrameworkElement child, string elementName)
        {
            //get parent item
            if (child.Name == elementName) return child;

            DependencyObject parentObject = GetParentObject(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            var parent = parentObject as FrameworkElement;
            if (parent == null)
            {
                return null;
            }
            else if (parent.Name == elementName)
            {
                return parent;
            }
            else
            {
                return FindParentElement(parent, elementName);
            }
        }

        /// <summary>
        /// This method is an alternative to WPF's
        /// <see cref="VisualTreeHelper.GetParent"/> method, which also
        /// supports content elements. Keep in mind that for content element,
        /// this method falls back to the logical tree of the element!
        /// </summary>
        /// <param name="child">The item to be processed.</param>
        /// <returns>The submitted item's parent, if available. Otherwise
        /// null.</returns>
        public static DependencyObject GetParentObject(this DependencyObject child)
        {
            if (child == null) return null;

            //handle content elements separately
            var contentElement = child as ContentElement;
            if (contentElement != null)
            {
                DependencyObject parent = ContentOperations.GetParent(contentElement);
                if (parent != null) return parent;

                var fce = contentElement as FrameworkContentElement;
                return fce != null ? fce.Parent : null;
            }

            //also try searching for parent in framework elements (such as DockPanel, etc)
            var frameworkElement = child as FrameworkElement;
            if (frameworkElement != null)
            {
                DependencyObject parent = frameworkElement.Parent;
                if (parent != null) return parent;
            }

            //if it's not a ContentElement/FrameworkElement, rely on VisualTreeHelper
            return VisualTreeHelper.GetParent(child);
        }

        /// <summary>
        /// Finds a Child of a given item in the visual tree.
        /// </summary>
        /// <param name="parent">A direct parent of the queried item.</param>
        /// <typeparam name="T">The type of the queried item.</typeparam>
        /// <param name="childName">x:Name or Name of child. </param>
        /// <returns>
        /// The first parent item that matches the submitted type parameter.
        /// If not matching item can be found,
        /// a null parent is being returned.
        /// </returns>
        /// <remarks>
        /// http://stackoverflow.com/a/1759923/1188513
        /// </remarks>
        public static T FindChild<T>(this DependencyObject parent, string childName)
            where T : DependencyObject
        {
            if (parent == null) return null;

            T foundChild = null;

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var childType = child as T;
                if (childType == null)
                {
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement == null || frameworkElement.Name != childName) continue;
                    foundChild = (T) child;
                    break;
                }
                else
                {
                    foundChild = (T) child;
                    break;
                }
            }

            return foundChild;
        }

        /// <summary>
        /// Execute synchronously an action on each item of an IEnumerable source
        /// </summary>
        /// <typeparam name="TSource">Type of the resource to process the task asynchronously</typeparam>
        /// <typeparam name="TResult">Type of the asynchronously computed result</typeparam>
        /// <param name="source"></param>
        /// <param name="taskSelector">Task to process asynchronously</param>
        /// <param name="resultProcessor">Action to process after completion</param>
        /// <returns></returns>
        public static Task ForEachAsync<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, Task<TResult>> taskSelector, Action<TSource, TResult> resultProcessor)
        {
            var oneAtATime = new SemaphoreSlim(5, 10);
            return Task.WhenAll(
                from item in source
                select ProcessAsync(item, taskSelector, resultProcessor, oneAtATime));
        }

        /// <summary>
        /// Process asynchronously a task with a semaphore limit
        /// </summary>
        /// <typeparam name="TSource">Type of the resource to process the task asynchronously</typeparam>
        /// <typeparam name="TResult">Type of the asynchronously computed result</typeparam>
        /// <param name="item"></param>
        /// <param name="taskSelector">Task to process asynchronously</param>
        /// <param name="resultProcessor">Action to process after completion</param>
        /// <param name="oneAtATime">Semaphore limit</param>
        /// <returns></returns>
        private static async Task ProcessAsync<TSource, TResult>(
            TSource item,
            Func<TSource, Task<TResult>> taskSelector, Action<TSource, TResult> resultProcessor,
            SemaphoreSlim oneAtATime)
        {
            var result = await taskSelector(item);
            await oneAtATime.WaitAsync();
            try
            {
                resultProcessor(item, result);
            }
            finally
            {
                oneAtATime.Release();
            }
        }
    }
}