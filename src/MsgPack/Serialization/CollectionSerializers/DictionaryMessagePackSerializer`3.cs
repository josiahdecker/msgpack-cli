#region -- License Terms --
// 
// MessagePack for CLI
// 
// Copyright (C) 2015 FUJIWARA, Yusuke
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// 
#endregion -- License Terms --

#if UNITY_STANDALONE || UNITY_WEBPLAYER || UNITY_WII || UNITY_IPHONE || UNITY_ANDROID || UNITY_PS3 || UNITY_XBOX360 || UNITY_FLASH || UNITY_BKACKBERRY || UNITY_WINRT
#define UNITY
#endif

using System;
#if UNITY
using System.Collections;
#endif // UNITY
using System.Collections.Generic;
#if UNITY
using System.Reflection;
#endif // UNITY
using System.Runtime.Serialization;

namespace MsgPack.Serialization.CollectionSerializers
{
	/// <summary>
	///		Provides basic features for generic dictionary serializers.
	/// </summary>
	/// <typeparam name="TDictionary">The type of the dictionary.</typeparam>
	/// <typeparam name="TKey">The type of the key of dictionary.</typeparam>
	/// <typeparam name="TValue">The type of the value of dictionary.</typeparam>
	/// <remarks>
	///		This class provides framework to implement variable collection serializer, and this type seals some virtual members to maximize future backward compatibility.
	///		If you cannot use this class, you can implement your own serializer which inherits <see cref="MessagePackSerializer{T}"/> and implements <see cref="ICollectionInstanceFactory"/>.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "By design" )]
	public abstract class DictionaryMessagePackSerializer<TDictionary, TKey, TValue> : MessagePackSerializer<TDictionary>, ICollectionInstanceFactory
		where TDictionary : IDictionary<TKey, TValue>
	{
		private readonly MessagePackSerializer<TKey> _keySerializer;
		private readonly MessagePackSerializer<TValue> _valueSerializer;

		/// <summary>
		///		Initializes a new instance of the <see cref="DictionaryMessagePackSerializer{TDictionary, TKey, TValue}"/> class.
		/// </summary>
		/// <param name="ownerContext">A <see cref="SerializationContext"/> which owns this serializer.</param>
		/// <param name="schema">
		///		The schema for collection itself or its items for the member this instance will be used to. 
		///		<c>null</c> will be considered as <see cref="PolymorphismSchema.Default"/>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		///		<paramref name="ownerContext"/> is <c>null</c>.
		/// </exception>
		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods", MessageId = "0", Justification = "Validated by base .ctor" )]
		protected DictionaryMessagePackSerializer( SerializationContext ownerContext, PolymorphismSchema schema )
			: base( ownerContext )
		{
			var safeSchema = schema ?? PolymorphismSchema.Default;
			this._keySerializer = ownerContext.GetSerializer<TKey>( safeSchema.KeySchema );
			this._valueSerializer = ownerContext.GetSerializer<TValue>( safeSchema.ItemSchema );
		}

		/// <summary>
		///		Serializes specified object with specified <see cref="Packer"/>.
		/// </summary>
		/// <param name="packer"><see cref="Packer"/> which packs values in <paramref name="objectTree"/>. This value will not be <c>null</c>.</param>
		/// <param name="objectTree">Object to be serialized.</param>
		/// <exception cref="SerializationException">
		///		<typeparamref name="TDictionary"/> is not serializable etc.
		/// </exception>
		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods", MessageId = "0", Justification = "Validated by caller in base class" )]
		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods", MessageId = "1", Justification = "Validated by caller in base class" )]
		protected internal sealed override void PackToCore( Packer packer, TDictionary objectTree )
		{
#if ( !UNITY && !XAMIOS ) || AOT_CHECK
			packer.PackMapHeader( objectTree.Count );
			foreach ( var item in objectTree )
			{
				this._keySerializer.PackTo( packer, item.Key );
				this._valueSerializer.PackTo( packer, item.Value );
			}
#else
			// .constraind call for TDictionary.get_Count/TDictionary.GetEnumerator() causes AOT error.
			// So use cast and invoke as normal call (it might cause boxing, but most collection should be reference type).
			packer.PackMapHeader( ( objectTree as IDictionary<TKey, TValue> ).Count );
			foreach ( var item in objectTree as IEnumerable<KeyValuePair<TKey,TValue>> )
			{
				this._keySerializer.PackTo( packer, item.Key );
				this._valueSerializer.PackTo( packer, item.Value );
			}
#endif // ( !UNITY && !XAMIOS ) || AOT_CHECK
		}

		/// <summary>
		///		Deserializes object with specified <see cref="Unpacker"/>.
		/// </summary>
		/// <param name="unpacker"><see cref="Unpacker"/> which unpacks values of resulting object tree. This value will not be <c>null</c>.</param>
		/// <returns>Deserialized object.</returns>
		/// <exception cref="SerializationException">
		///		Failed to deserialize object due to invalid unpacker state, stream content, or so.
		/// </exception>
		/// <exception cref="MessageTypeException">
		///		Failed to deserialize object due to invalid unpacker state, stream content, or so.
		/// </exception>
		/// <exception cref="InvalidMessagePackStreamException">
		///		Failed to deserialize object due to invalid unpacker state, stream content, or so.
		/// </exception>
		/// <exception cref="NotSupportedException">
		///		<typeparamref name="TDictionary"/> is abstract type.
		/// </exception>
		/// <remarks>
		///		This method invokes <see cref="CreateInstance(int)"/>, and then fill deserialized items to resultong collection.
		/// </remarks>
		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods", MessageId = "0", Justification = "Validated by caller in base class" )]
		protected internal sealed override TDictionary UnpackFromCore( Unpacker unpacker )
		{
			if ( !unpacker.IsMapHeader )
			{
				throw SerializationExceptions.NewIsNotArrayHeader();
			}

			return this.InternalUnpackFromCore( unpacker );
		}

		internal virtual TDictionary InternalUnpackFromCore( Unpacker unpacker )
		{
			var itemsCount = UnpackHelpers.GetItemsCount( unpacker );
			var collection = this.CreateInstance( itemsCount );
			this.UnpackToCore( unpacker, collection, itemsCount );
			return collection;
		}

		/// <summary>
		///		Creates a new collection instance with specified initial capacity.
		/// </summary>
		/// <param name="initialCapacity">
		///		The initial capacy of creating collection.
		///		Note that this parameter may <c>0</c> for non-empty collection.
		/// </param>
		/// <returns>
		/// New collection instance. This value will not be <c>null</c>.
		/// </returns>
		/// <remarks>
		///		An author of <see cref="Unpacker" /> could implement unpacker for non-MessagePack format,
		///		so implementer of this interface should not rely on that <paramref name="initialCapacity" /> reflects actual items count.
		///		For example, JSON unpacker cannot supply collection items count efficiently.
		/// </remarks>
		/// <seealso cref="ICollectionInstanceFactory.CreateInstance"/>
		protected abstract TDictionary CreateInstance( int initialCapacity );

		object ICollectionInstanceFactory.CreateInstance( int initialCapacity )
		{
			return this.CreateInstance( initialCapacity );
		}

		/// <summary>
		///		Deserializes collection items with specified <see cref="Unpacker"/> and stores them to <paramref name="collection"/>.
		/// </summary>
		/// <param name="unpacker"><see cref="Unpacker"/> which unpacks values of resulting object tree. This value will not be <c>null</c>.</param>
		/// <param name="collection">Collection that the items to be stored. This value will not be <c>null</c>.</param>
		/// <exception cref="SerializationException">
		///		Failed to deserialize object due to invalid unpacker state, stream content, or so.
		/// </exception>
		/// <exception cref="NotSupportedException">
		///		<typeparamref name="TDictionary"/> is not collection.
		/// </exception>
		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods", MessageId = "0", Justification = "Validated by caller in base class" )]
		[System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods", MessageId = "1", Justification = "Validated by caller in base class" )]
		protected internal sealed override void UnpackToCore( Unpacker unpacker, TDictionary collection )
		{
			if ( !unpacker.IsMapHeader )
			{
				throw SerializationExceptions.NewIsNotArrayHeader();
			}

			this.UnpackToCore( unpacker, collection, UnpackHelpers.GetItemsCount( unpacker ) );
		}

		private void UnpackToCore( Unpacker unpacker, TDictionary collection, int itemsCount )
		{
			for ( int i = 0; i < itemsCount; i++ )
			{
				if ( !unpacker.Read() )
				{
					throw SerializationExceptions.NewMissingItem( i );
				}

				TKey key;
				if ( !unpacker.IsArrayHeader && !unpacker.IsMapHeader )
				{
					key = this._keySerializer.UnpackFrom( unpacker );
				}
				else
				{
					using ( var subtreeUnpacker = unpacker.ReadSubtree() )
					{
						key = this._keySerializer.UnpackFrom( subtreeUnpacker );
					}
				}

				if ( !unpacker.Read() )
				{
					throw SerializationExceptions.NewMissingItem( i );
				}


				TValue value;
				if ( !unpacker.IsArrayHeader && !unpacker.IsMapHeader )
				{
					value = this._valueSerializer.UnpackFrom( unpacker );
				}
				else
				{
					using ( var subtreeUnpacker = unpacker.ReadSubtree() )
					{
						value = this._valueSerializer.UnpackFrom( subtreeUnpacker );
					}
				}

#if ( !UNITY && !XAMIOS ) || AOT_CHECK
				collection.Add( key, value );
#else
				// .constraind call for TDictionary.Add causes AOT error.
				// So use cast and invoke as normal call (it might cause boxing, but most collection should be reference type).
				( collection as IDictionary<TKey, TValue> ).Add( key, value );
#endif // ( !UNITY && !XAMIOS ) || AOT_CHECK
			}
		}
	}

#if UNITY
	internal abstract class UnityDictionaryMessagePackSerializer : NonGenericMessagePackSerializer,
		ICollectionInstanceFactory
	{
		private readonly IMessagePackSingleObjectSerializer _keySerializer;
		private readonly IMessagePackSingleObjectSerializer _valueSerializer;
		private readonly MethodInfo _add;
		private readonly MethodInfo _getCount;
		private readonly MethodInfo _getKey;
		private readonly MethodInfo _getValue;

		protected UnityDictionaryMessagePackSerializer(
			SerializationContext ownerContext,
			Type targetType,
			Type keyType,
			Type valueType,
			CollectionTraits traits,
			PolymorphismSchema schema
		)
			: base( ownerContext, targetType )
		{
			var safeSchema = schema ?? PolymorphismSchema.Default;
			this._keySerializer = ownerContext.GetSerializer( keyType, safeSchema.KeySchema );
			this._valueSerializer = ownerContext.GetSerializer( valueType, safeSchema.ItemSchema );
			this._add = traits.AddMethod;
			this._getCount = traits.CountPropertyGetter;
			this._getKey = traits.ElementType.GetProperty( "Key" ).GetGetMethod();
			this._getValue = traits.ElementType.GetProperty( "Value" ).GetGetMethod();
		}

		protected internal override sealed void PackToCore( Packer packer, object objectTree )
		{
			packer.PackMapHeader( ( int )this._getCount.InvokePreservingExceptionType( objectTree ) );
			// ReSharper disable once PossibleNullReferenceException
			foreach ( var item in objectTree as IEnumerable )
			{
				this._keySerializer.PackTo( packer, this._getKey.InvokePreservingExceptionType( item ) );
				this._valueSerializer.PackTo( packer, this._getValue.InvokePreservingExceptionType( item ) );
			}
		}

		protected internal override sealed object UnpackFromCore( Unpacker unpacker )
		{
			if ( !unpacker.IsMapHeader )
			{
				throw SerializationExceptions.NewIsNotArrayHeader();
			}

			return this.InternalUnpackFromCore( unpacker );
		}

		internal virtual object InternalUnpackFromCore( Unpacker unpacker )
		{
			var itemsCount = UnpackHelpers.GetItemsCount( unpacker );
			var collection = this.CreateInstance( itemsCount );
			this.UnpackToCore( unpacker, collection, itemsCount );
			return collection;
		}

		protected abstract object CreateInstance( int initialCapacity );

		object ICollectionInstanceFactory.CreateInstance( int initialCapacity )
		{
			return this.CreateInstance( initialCapacity );
		}

		protected internal override sealed void UnpackToCore( Unpacker unpacker, object collection )
		{
			if ( !unpacker.IsMapHeader )
			{
				throw SerializationExceptions.NewIsNotArrayHeader();
			}

			this.UnpackToCore( unpacker, collection, UnpackHelpers.GetItemsCount( unpacker ) );
		}

		private void UnpackToCore( Unpacker unpacker, object collection, int itemsCount )
		{
			for ( int i = 0; i < itemsCount; i++ )
			{
				if ( !unpacker.Read() )
				{
					throw SerializationExceptions.NewMissingItem( i );
				}

				object key;
				if ( !unpacker.IsArrayHeader && !unpacker.IsMapHeader )
				{
					key = this._keySerializer.UnpackFrom( unpacker );
				}
				else
				{
					using ( var subtreeUnpacker = unpacker.ReadSubtree() )
					{
						key = this._keySerializer.UnpackFrom( subtreeUnpacker );
					}
				}

				if ( !unpacker.Read() )
				{
					throw SerializationExceptions.NewMissingItem( i );
				}


				object value;
				if ( !unpacker.IsArrayHeader && !unpacker.IsMapHeader )
				{
					value = this._valueSerializer.UnpackFrom( unpacker );
				}
				else
				{
					using ( var subtreeUnpacker = unpacker.ReadSubtree() )
					{
						value = this._valueSerializer.UnpackFrom( subtreeUnpacker );
					}
				}

				this._add.InvokePreservingExceptionType( collection, key, value );
			}
		}
	}
#endif // UNITY
}