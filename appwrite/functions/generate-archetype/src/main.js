const { Client, Databases, Storage, ID } = require('node-appwrite');
const OpenAI = require('openai');

const ARCHETYPE_SYSTEM_PROMPT = `You are a game design assistant for a basketball card game.
Goal: Given a player name (and optional stat hints), infer a plausible play style archetype and generate a UNIQUE "Archetype Crest" design spec.

Constraints:
- Do NOT use NBA/team logos, jerseys, player likeness, real photos, or trademarked symbols.
- The crest must be abstract, original, and collectible: geometric sigil + iconography + patterns + materials + frame notes.
- Must be safe for an unlicensed game: no endorsement implications.
- Output MUST be valid JSON only, matching the schema exactly.
- If you are unsure of the player's style, set "confidence":"low" and infer from provided stat hints or generic role assumptions.`;

const ARCHETYPE_SCHEMA = {
    "player_name": "string",
    "confidence": "high|medium|low",
    "play_style_summary": "string (2-3 sentences describing play style)",
    "archetype": "string (e.g., 'Scoring Machine', 'Defensive Anchor', 'Floor General')",
    "sub_archetype": "string (more specific variant)",
    "crest_design": {
        "core_shape": "string",
        "primary_motif": "string",
        "secondary_motifs": ["string"],
        "pattern_language": "string",
        "materials": ["string"],
        "color_story": "string"
    },
    "image_prompt": "string (image prompt for crest - abstract art only, no people)"
};

function stableSeed(text) {
    let hash = 0;
    for (let i = 0; i < text.length; i++) {
        const char = text.charCodeAt(i);
        hash = ((hash << 5) - hash) + char;
        hash = hash & hash;
    }
    return Math.abs(hash).toString(16).substring(0, 16);
}

// Generate image using ModelsLab API (much cheaper than DALL-E)
async function generateImageWithModelsLab(prompt, apiKey, log, error) {
    const url = 'https://modelslab.com/api/v6/images/text2img';

    const enhancedPrompt = `Premium holographic trading card crest design. ${prompt}. Abstract geometric art, no text, no people, centered composition, dark background, metallic accents, high quality, detailed.`;

    const payload = {
        key: apiKey,
        model_id: "sdxl",  // Use SDXL model - stable and widely available
        prompt: enhancedPrompt,
        negative_prompt: "text, words, letters, numbers, signature, watermark, human, person, face, body, realistic photo, blurry, low quality",
        width: "512",
        height: "512",
        samples: "1",
        num_inference_steps: "30",
        guidance_scale: 7.5,
        safety_checker: "no",
        enhance_prompt: "yes",
        seed: null,
        webhook: null,
        track_id: null
    };

    log(`Calling ModelsLab text2img API...`);

    const response = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        throw new Error(`ModelsLab API error: ${response.status}`);
    }

    const result = await response.json();
    log(`ModelsLab response status: ${result.status}`);

    if (result.status === 'error') {
        throw new Error(`ModelsLab error: ${result.message || result.messege || 'Unknown error'}`);
    }

    // Handle async processing
    if (result.status === 'processing') {
        log(`Image processing, ETA: ${result.eta} seconds`);
        const fetchUrl = result.fetch_result;

        // Wait and poll for result
        await new Promise(resolve => setTimeout(resolve, (result.eta || 10) * 1000));

        for (let attempt = 0; attempt < 10; attempt++) {
            const pollResponse = await fetch(fetchUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ key: apiKey })
            });

            const pollResult = await pollResponse.json();
            log(`Poll attempt ${attempt + 1}: ${pollResult.status}`);

            if (pollResult.status === 'success' && pollResult.output && pollResult.output.length > 0) {
                return pollResult.output[0];
            }

            if (pollResult.status === 'error') {
                throw new Error(`ModelsLab polling error: ${pollResult.message}`);
            }

            // Wait 3 seconds before next poll
            await new Promise(resolve => setTimeout(resolve, 3000));
        }

        throw new Error('ModelsLab timeout: Image generation took too long');
    }

    // Immediate result
    if (result.status === 'success' && result.output && result.output.length > 0) {
        return result.output[0];
    }

    throw new Error('ModelsLab: No image URL in response');
}

module.exports = async function (context) {
    const { req, res, log, error } = context;

    // Initialize clients
    const client = new Client()
        .setEndpoint(process.env.APPWRITE_FUNCTION_API_ENDPOINT)
        .setProject(process.env.APPWRITE_FUNCTION_PROJECT_ID)
        .setKey(process.env.APPWRITE_API_KEY);

    const databases = new Databases(client);
    const storage = new Storage(client);

    const openai = new OpenAI({
        apiKey: process.env.OPENAI_API_KEY
    });

    const databaseId = process.env.DATABASE_ID;
    const archetypesCollection = process.env.ARCHETYPES_COLLECTION_ID;
    const crestsBucket = process.env.CRESTS_BUCKET_ID;
    const modelsLabApiKey = process.env.MODELSLAB_API_KEY;

    try {
        // Parse request body
        let body;
        if (typeof req.body === 'string') {
            body = JSON.parse(req.body);
        } else {
            body = req.body;
        }

        const { playerId, playerName, statHints } = body;

        if (!playerId || !playerName) {
            return res.json({ success: false, error: 'Missing playerId or playerName' });
        }

        log(`Generating archetype for: ${playerName} (${playerId})`);

        // Check if archetype already exists
        try {
            const existing = await databases.getDocument(databaseId, archetypesCollection, playerId);
            if (existing && existing.crestImageUrl) {
                log(`Archetype already exists for ${playerName}`);
                return res.json({
                    success: true,
                    cached: true,
                    data: {
                        archetype: existing.archetype,
                        subArchetype: existing.subArchetype,
                        playStyleSummary: existing.playStyleSummary,
                        crestImageUrl: existing.crestImageUrl,
                        confidence: existing.confidence
                    }
                });
            }
        } catch (e) {
            // Document doesn't exist, continue to generate
            log(`No existing archetype found, generating new one...`);
        }

        const seed = stableSeed(playerName);

        // Step 1: Generate archetype data with GPT-4
        const userPrompt = `
Player name: ${playerName}
Optional stat hints: ${statHints || 'N/A'}
Crest seed: ${seed}

Task:
1) Infer a plausible play style and classify into an archetype + sub-archetype.
2) Generate a unique archetype crest design spec (abstract, non-infringing).
3) Produce an image prompt for a premium holographic trading-card crest graphic.

JSON output schema (MUST follow exactly):
${JSON.stringify(ARCHETYPE_SCHEMA, null, 2)}

Important:
- Output JSON only. No markdown, no extra text.
- Never include team names, NBA branding, or any real player likeness details.
- The image_prompt should describe ONLY the crest artwork (not a person).
`.trim();

        log(`Calling GPT-4...`);
        const gptResponse = await openai.chat.completions.create({
            model: 'gpt-4o-mini',
            messages: [
                { role: 'system', content: ARCHETYPE_SYSTEM_PROMPT },
                { role: 'user', content: userPrompt }
            ],
            max_tokens: 1200,
            temperature: 0.8
        });

        const gptContent = gptResponse.choices[0].message.content;
        let archetypeData;
        try {
            archetypeData = JSON.parse(gptContent);
        } catch (parseErr) {
            error(`Failed to parse GPT response: ${gptContent}`);
            return res.json({ success: false, error: 'Failed to parse archetype data' });
        }

        log(`Archetype generated: ${archetypeData.archetype}`);

        // Step 2: Generate crest image with ModelsLab (cheaper) or DALL-E (fallback)
        let crestImageUrl = null;
        if (archetypeData.image_prompt) {
            // Try ModelsLab first (much cheaper: ~$0.002/image vs $0.016/image)
            if (modelsLabApiKey) {
                log(`Generating crest image with ModelsLab...`);
                try {
                    crestImageUrl = await generateImageWithModelsLab(
                        archetypeData.image_prompt,
                        modelsLabApiKey,
                        log,
                        error
                    );
                    log(`ModelsLab image generated successfully`);
                } catch (mlErr) {
                    error(`ModelsLab error: ${mlErr.message}`);
                    // Fall through to DALL-E
                }
            }

            // Fallback to DALL-E if ModelsLab fails or not configured
            if (!crestImageUrl) {
                log(`Generating crest image with DALL-E (fallback)...`);
                try {
                    const imageResponse = await openai.images.generate({
                        model: 'dall-e-2',
                        prompt: `Premium holographic trading card crest design. ${archetypeData.image_prompt}. Abstract geometric art, no text, no people, centered composition, dark background, metallic accents.`,
                        n: 1,
                        size: '256x256'
                    });

                    crestImageUrl = imageResponse.data[0].url;
                    log(`DALL-E image generated successfully`);
                } catch (imgErr) {
                    error(`DALL-E error: ${imgErr.message}`);
                    // Continue without image
                }
            }
        }

        // Step 3: Save to database
        // Note: $createdAt is auto-generated by Appwrite, don't include createdAt
        const documentData = {
            playerId: playerId,
            playerName: playerName,
            archetype: archetypeData.archetype || '',
            subArchetype: archetypeData.sub_archetype || '',
            playStyleSummary: archetypeData.play_style_summary || '',
            confidence: archetypeData.confidence || 'medium',
            crestImageUrl: crestImageUrl || '',
            crestSeed: seed,
            imagePrompt: archetypeData.image_prompt || ''
        };

        try {
            await databases.createDocument(
                databaseId,
                archetypesCollection,
                playerId,
                documentData
            );
            log(`Saved archetype to database`);
        } catch (saveErr) {
            // Document might already exist, try update
            try {
                await databases.updateDocument(
                    databaseId,
                    archetypesCollection,
                    playerId,
                    documentData
                );
                log(`Updated existing archetype in database`);
            } catch (updateErr) {
                error(`Failed to save archetype: ${updateErr.message}`);
            }
        }

        return res.json({
            success: true,
            data: {
                archetype: archetypeData.archetype,
                subArchetype: archetypeData.sub_archetype,
                playStyleSummary: archetypeData.play_style_summary,
                crestImageUrl: crestImageUrl,
                confidence: archetypeData.confidence
            }
        });
    } catch (err) {
        error(`Error: ${err.message}`);
        return res.json({
            success: false,
            error: err.message
        });
    }
};
