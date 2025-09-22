﻿using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Orleans.Providers.MongoDB.Utils;

namespace Orleans.Providers.MongoDB.Membership.Store.Multiple
{
    public sealed class TableVersionCollection : CollectionBase<TableVersionDocument>
    {
        private static readonly TableVersion NotFound = new TableVersion(0, "0");
        private readonly string collectionPrefix;

        public TableVersionCollection(
            IMongoClient mongoClient,
            string databaseName,
            string collectionPrefix,
            Action<MongoCollectionSettings> collectionConfigurator,
            bool createShardKey)
            : base(mongoClient, databaseName, collectionConfigurator, createShardKey)
        {
            this.collectionPrefix = collectionPrefix;
        }

        protected override string CollectionName()
        {
            return $"{collectionPrefix}OrleansMembershipV3_TableVersion";
        }

        public Task DeleteAsync(IClientSessionHandle session, string deploymentId)
        {
            return Collection.DeleteOneAsync(session, x => x.DeploymentId == deploymentId);
        }

        public async Task<TableVersion> GetTableVersionAsync(IClientSessionHandle session, string deploymentId)
        {
            var deployment = await Collection.Find(session, x => x.DeploymentId == deploymentId).FirstOrDefaultAsync();

            if (deployment == null)
            {
                return NotFound;
            }

            return deployment.ToTableVersion();
        }

        public async Task<bool> UpsertAsync(IClientSessionHandle session, TableVersion tableVersion, string deploymentId)
        {
            var update = TableVersionDocument.Create(deploymentId, tableVersion);

            try
            {
                await Collection.ReplaceOneAsync(session,
                    x => x.DeploymentId == deploymentId && x.VersionEtag == tableVersion.VersionEtag,
                    update,
                    UpsertReplace);

                return true;
            }
            catch (MongoException ex)
            {
                if (ex.IsDuplicateKey())
                {
                    return false;
                }
                throw;
            }
        }
    }
}
