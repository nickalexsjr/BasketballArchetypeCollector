const { Client, Databases, Query } = require('node-appwrite');

module.exports = async function (context) {
    const { req, res, log, error } = context;

    // Initialize Appwrite client
    const client = new Client()
        .setEndpoint(process.env.APPWRITE_FUNCTION_API_ENDPOINT)
        .setProject(process.env.APPWRITE_FUNCTION_PROJECT_ID)
        .setKey(process.env.APPWRITE_API_KEY);

    const databases = new Databases(client);
    const databaseId = process.env.DATABASE_ID;

    try {
        // Parse request body
        let body;
        if (typeof req.body === 'string') {
            body = JSON.parse(req.body);
        } else {
            body = req.body;
        }

        const collectionId = body.collection;
        if (!collectionId) {
            return res.json({ success: false, error: 'Missing collection parameter' });
        }

        log(`Fetching all documents from collection: ${collectionId}`);

        // Fetch all documents with pagination
        const allDocuments = [];
        const limit = 100;
        let offset = 0;
        let hasMore = true;

        while (hasMore) {
            const response = await databases.listDocuments(
                databaseId,
                collectionId,
                [
                    Query.limit(limit),
                    Query.offset(offset)
                ]
            );

            allDocuments.push(...response.documents);
            log(`Fetched ${response.documents.length} documents (total: ${allDocuments.length}/${response.total})`);

            if (allDocuments.length >= response.total || response.documents.length < limit) {
                hasMore = false;
            } else {
                offset += limit;
            }
        }

        log(`Total documents fetched: ${allDocuments.length}`);

        return res.json({
            success: true,
            total: allDocuments.length,
            documents: allDocuments
        });
    } catch (err) {
        error(`Error: ${err.message}`);
        return res.json({
            success: false,
            error: err.message
        });
    }
};
