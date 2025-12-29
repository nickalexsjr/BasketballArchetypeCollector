/**
 * Generate Archetype Function
 *
 * CRITICAL: This function requires Appwrite timeout set to 120+ seconds!
 *
 * In Appwrite Console:
 * 1. Go to Functions → generate-archetype → Settings
 * 2. Find "Timeout" setting (default is 30 seconds)
 * 3. Change to 120 or 180 seconds
 * 4. Save and REDEPLOY the function
 *
 * ModelsLab image generation works 100% - the 30s failure is Appwrite
 * killing the function before ModelsLab can complete.
 */

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

// Generate image using ModelsLab API with timeout
// NOTE: Appwrite function timeout must be set to 120+ seconds in the Console!
// Go to Functions → generate-archetype → Settings → Timeout → set to 120 or 180
async function generateImageWithModelsLab(prompt, apiKey, log, error, maxWaitMs = 60000) {
    const url = 'https://modelslab.com/api/v6/images/text2img';
    const startTime = Date.now();

    const enhancedPrompt = `Premium holographic trading card crest design. ${prompt}. Abstract geometric art, no text, no people, centered composition, dark background, metallic accents, high quality, detailed.`;

    const payload = {
        key: apiKey,
        model_id: "sdxl",  // SDXL model
        prompt: enhancedPrompt,
        negative_prompt: "text, words, letters, numbers, signature, watermark, human, person, face, body, realistic photo, blurry, low quality",
        width: "512",
        height: "512",
        samples: "1",
        num_inference_steps: "25",
        guidance_scale: 7.5,
        safety_checker: "no",
        enhance_prompt: "yes",
        seed: null,
        webhook: null,
        track_id: null
    };

    log(`Calling ModelsLab text2img API...`);

    // Use AbortController to enforce timeout on fetch
    const controller = new AbortController();
    const fetchTimeout = setTimeout(() => controller.abort(), 30000); // 30s for initial request

    let response;
    try {
        response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
            signal: controller.signal
        });
    } finally {
        clearTimeout(fetchTimeout);
    }

    if (!response.ok) {
        throw new Error(`ModelsLab API error: ${response.status}`);
    }

    const result = await response.json();
    log(`ModelsLab response status: ${result.status}`);

    if (result.status === 'error') {
        throw new Error(`ModelsLab error: ${result.message || result.messege || 'Unknown error'}`);
    }

    // Handle async processing with strict timeout
    if (result.status === 'processing') {
        log(`Image processing, ETA: ${result.eta}s (max wait: ${maxWaitMs/1000}s)`);
        const fetchUrl = result.fetch_result;

        // Initial wait - min of ETA or remaining time, capped at 15s
        const remainingMs = maxWaitMs - (Date.now() - startTime);
        const initialWait = Math.min((result.eta || 10) * 1000, 15000, remainingMs);

        if (initialWait <= 0) {
            throw new Error('ModelsLab timeout: no time remaining');
        }

        await new Promise(resolve => setTimeout(resolve, initialWait));

        // Poll with strict timeout check
        for (let attempt = 0; attempt < 4; attempt++) {
            const elapsed = Date.now() - startTime;
            if (elapsed > maxWaitMs) {
                throw new Error(`ModelsLab timeout: ${elapsed}ms > ${maxWaitMs}ms limit`);
            }

            const pollResponse = await fetch(fetchUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ key: apiKey })
            });

            const pollResult = await pollResponse.json();
            log(`Poll ${attempt + 1}: ${pollResult.status} (${Math.round(elapsed/1000)}s elapsed)`);

            if (pollResult.status === 'success' && pollResult.output && pollResult.output.length > 0) {
                log(`ModelsLab completed in ${Math.round(elapsed/1000)}s`);
                return pollResult.output[0];
            }

            if (pollResult.status === 'error') {
                throw new Error(`ModelsLab polling error: ${pollResult.message}`);
            }

            // Check if we have time for another poll (3s wait + buffer)
            if (Date.now() - startTime + 4000 > maxWaitMs) {
                throw new Error('ModelsLab timeout: not enough time for next poll');
            }

            // Wait 3 seconds before next poll
            await new Promise(resolve => setTimeout(resolve, 3000));
        }

        throw new Error('ModelsLab timeout: max poll attempts reached');
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

    // OpenAI SDK with extended timeout (default is 10 minutes but let's be explicit)
    const openai = new OpenAI({
        apiKey: process.env.OPENAI_API_KEY,
        timeout: 120000,  // 120 seconds timeout for API calls
        maxRetries: 2     // Retry failed requests
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

        // Step 2: Generate crest image - try ModelsLab first (cheaper!), fallback to DALL-E after 30s
        let crestImageUrl = null;
        if (archetypeData.image_prompt) {
            // Try ModelsLab first (much cheaper: ~$0.002/image vs $0.016/image)
            // ModelsLab works 100% - the issue is Appwrite function timeout, not ModelsLab
            // IMPORTANT: Appwrite function timeout must be set to 120+ seconds in Console!
            // Go to: Functions → generate-archetype → Settings → Timeout → set to 120
            if (modelsLabApiKey) {
                log(`Generating crest image with ModelsLab (60s timeout)...`);
                try {
                    crestImageUrl = await generateImageWithModelsLab(
                        archetypeData.image_prompt,
                        modelsLabApiKey,
                        log,
                        error,
                        60000  // 60 second max wait for ModelsLab, then fallback to DALL-E
                    );
                    log(`ModelsLab image generated successfully`);
                } catch (mlErr) {
                    error(`ModelsLab error: ${mlErr.message}`);
                    // Fall through to DALL-E
                }
            }

            // Fallback to DALL-E if ModelsLab fails or times out
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
                    // Continue without image - we'll still save archetype data
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
