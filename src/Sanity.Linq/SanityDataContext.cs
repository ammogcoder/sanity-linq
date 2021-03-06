﻿// Copywrite 2018 Oslofjord Operations AS

// This file is part of Sanity LINQ (https://github.com/oslofjord/sanity-linq).

//  Sanity LINQ is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.

//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//  GNU General Public License for more details.

//  You should have received a copy of the GNU General Public License
//  along with this program.If not, see<https://www.gnu.org/licenses/>.


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sanity.Linq.DTOs;
using Sanity.Linq.CommonTypes;
using Sanity.Linq.Mutations;
using Sanity.Linq.BlockContent;

namespace Sanity.Linq
{
    /// <summary>
    /// Linq-to-Sanity Data Context.
    /// Handles intialization of SanityDbSets defined in inherited classes.
    /// </summary>
    public class SanityDataContext
    {

        private object _dsLock = new object();
        private ConcurrentDictionary<Type, SanityDocumentSet> _documentSets = new ConcurrentDictionary<Type, SanityDocumentSet>();

        internal bool IsShared { get; }

        public SanityClient Client { get; }

        public SanityMutationBuilder Mutations { get; }

        public JsonSerializerSettings SerializerSettings { get; }

        public SanityHtmlBuilder HtmlBuilder { get; set; }

        /// <summary>
        /// Create a new SanityDbContext using the specified options.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="isShared">Indicates that the context can be used by multiple SanityDocumentSets</param>
        public SanityDataContext(SanityOptions options, JsonSerializerSettings serializerSettings = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            SerializerSettings = serializerSettings ?? new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter> { new SanityReferenceTypeConverter() }
            };
            Client = new SanityClient(options, serializerSettings);
            Mutations = new SanityMutationBuilder(Client);
            HtmlBuilder = new SanityHtmlBuilder(options, null, SerializerSettings);
        }

       
        /// <summary>
        /// Create a new SanityDbContext using the specified options.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="isShared">Indicates that the context can be used by multiple SanityDocumentSets</param>
        internal SanityDataContext(SanityOptions options, bool isShared) : this(options)
        {
            IsShared = isShared;
        }
             

        /// <summary>
        /// Returns an IQueryable document set for specified type
        /// </summary>
        /// <typeparam name="TDoc"></typeparam>
        /// <returns></returns>
        public virtual SanityDocumentSet<TDoc> DocumentSet<TDoc>()
        {
            lock (_dsLock)
            {
                if (!_documentSets.ContainsKey(typeof(TDoc)))
                {
                    _documentSets[typeof(TDoc)] = new SanityDocumentSet<TDoc>(this);
                }
            }
            return _documentSets[(typeof(TDoc))] as SanityDocumentSet<TDoc>;
        }

        public virtual SanityDocumentSet<SanityImageAsset> Images => DocumentSet<SanityImageAsset>();

        public virtual SanityDocumentSet<SanityFileAsset> Files => DocumentSet<SanityFileAsset>();

        public virtual SanityDocumentSet<SanityDocument> Documents => DocumentSet<SanityDocument>();

        public virtual void ClearChanges()
        {
            Mutations.Clear();
        }

        /// <summary>
        /// Sends all changes registered on Document sets to Sanity as a transactional set of mutations.
        /// </summary>
        /// <param name="returnIds"></param>
        /// <param name="returnDocuments"></param>
        /// <param name="visibility"></param>
        /// <returns></returns>
        public async Task<SanityMutationResponse> CommitAsync(bool returnIds = false, bool returnDocuments = false, SanityMutationVisibility visibility = SanityMutationVisibility.Sync)
        {
            var result = await Client.CommitMutationsAsync(Mutations.Build(Client.SerializerSettings), returnIds, returnDocuments, visibility).ConfigureAwait(false);
            Mutations.Clear();
            return result;
        }

        /// <summary>
        /// Sends all changes registered on document sets of specified type to Sanity as a transactional set of mutations.
        /// </summary>
        /// <param name="returnIds"></param>
        /// <param name="returnDocuments"></param>
        /// <param name="visibility"></param>
        /// <returns></returns>
        public async Task<SanityMutationResponse<TDoc>> CommitAsync<TDoc>(bool returnIds = false, bool returnDocuments = false, SanityMutationVisibility visibility = SanityMutationVisibility.Sync)
        {
            var mutations = Mutations.For<TDoc>();
            if (mutations.Mutations.Count > 0)
            {
                var result = await Client.CommitMutationsAsync<TDoc>(mutations.Build(), returnIds, returnDocuments, visibility).ConfigureAwait(false);
                mutations.Clear();
                return result;
            }
            throw new Exception($"No pending changes for document type {typeof(TDoc)}");
        }

    }
}
