/**
 * Generate Archetype Function v5
 *
 * v5 changes:
 * - 6-LAYER FALLBACK image generation for 100% reliability:
 *   1. ModelsLab SDXL (primary, cheapest ~$0.002/image)
 *   2. OpenAI DALL-E 2 via native fetch (first backup ~$0.016/image)
 *   3. OpenAI DALL-E 2 via SDK (second backup, different approach)
 *   4. ModelsLab Realvis XL v4.0 (third backup, different queue)
 *   5. ModelsLab Juggernaut XL v8 (fourth backup, another queue)
 *   6. ModelsLab DreamShaper XL (fifth backup, last resort)
 * - Pre-warm endpoint (warmup=true) to wake up AI services without cost
 * - Supports parallel generation requests
 *
 * v3 changes (preserved):
 * - Stronger no-text enforcement in prompts (DALL-E and ModelsLab)
 * - Faster generation: 15 inference steps, 1.5s poll interval
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
- If you are unsure of the player's style, set "confidence":"low" and infer from provided stat hints or generic role assumptions.

CRITICAL for image_prompt:
- Make each crest WILDLY unique and visually stunning
- Use varied art styles: cyberpunk, ethereal, crystalline, volcanic, cosmic, ancient runes, neon, biomechanical, celestial, arcane
- Include specific unique elements: glowing orbs, energy fractals, sacred geometry, floating shards, plasma cores, runic circles
- Vary color palettes dramatically: not just gold/blue - use crimson/black, electric cyan, deep purple/magenta, emerald/silver, sunset orange
- Add dynamic elements: energy particles, light rays, swirling auras, crystalline formations, ethereal flames
- Each crest should feel like a legendary collectible artifact`;

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

/**
 * Call OpenAI Chat API using native fetch (no SDK).
 * This avoids the internal timeout issues in the OpenAI SDK.
 */
async function callOpenAIChatWithFetch(apiKey, messages, log) {
    log(`Calling OpenAI Chat API (native fetch)...`);

    const response = await fetch('https://api.openai.com/v1/chat/completions', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${apiKey}`
        },
        body: JSON.stringify({
            model: 'gpt-4o-mini',
            messages: messages,
            max_tokens: 1200,
            temperature: 0.8
        })
    });

    if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`OpenAI API error: ${response.status} - ${errorText}`);
    }

    const data = await response.json();
    return data.choices[0].message.content;
}

/**
 * Call DALL-E API using native fetch (no SDK).
 */
async function callDallEWithFetch(apiKey, prompt, log) {
    log(`Calling DALL-E API (native fetch)...`);

    const response = await fetch('https://api.openai.com/v1/images/generations', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${apiKey}`
        },
        body: JSON.stringify({
            model: 'dall-e-2',
            prompt: `Premium holographic trading card crest design. ${prompt}. Abstract geometric art, absolutely no text no letters no words no numbers no writing no typography, no people, centered composition, dark background, metallic accents.`,
            n: 1,
            size: '256x256'
        })
    });

    if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`DALL-E API error: ${response.status} - ${errorText}`);
    }

    const data = await response.json();
    return data.data[0].url;
}

/**
 * Call DALL-E API using OpenAI SDK (alternative approach).
 * This is a backup in case native fetch has issues.
 */
async function callDallEWithSDK(apiKey, prompt, log) {
    log(`Calling DALL-E API (OpenAI SDK)...`);

    const openai = new OpenAI({ apiKey });

    const imageResponse = await openai.images.generate({
        model: 'dall-e-2',
        prompt: `Premium holographic trading card crest design. ${prompt}. Abstract geometric art, absolutely no text no letters no words no numbers no writing no typography, no people, centered composition, dark background, metallic accents.`,
        n: 1,
        size: '256x256'
    });

    return imageResponse.data[0].url;
}

/**
 * Generate image using ModelsLab API with specified model.
 * Supports multiple models for fallback reliability.
 *
 * @param {string} prompt - The image prompt
 * @param {string} apiKey - ModelsLab API key
 * @param {string} modelId - Model to use (e.g., 'sdxl', 'realvis-xl-v40', 'juggernaut-xl-v8', 'dreamshaper-xl')
 * @param {function} log - Logging function
 * @param {function} error - Error logging function
 * @param {number} maxWaitMs - Maximum wait time in milliseconds
 */
async function generateImageWithModelsLab(prompt, apiKey, modelId, log, error, maxWaitMs = 45000) {
    const url = 'https://modelslab.com/api/v6/images/text2img';
    const startTime = Date.now();

    // Random style elements for variety
    const styles = ['ultra detailed', 'hyperrealistic render', '8k octane render', 'unreal engine 5', 'cinematic lighting'];
    const finishes = ['holographic iridescent', 'chrome metallic', 'glowing neon edges', 'crystalline refractions', 'ethereal glow'];
    const randomStyle = styles[Math.floor(Math.random() * styles.length)];
    const randomFinish = finishes[Math.floor(Math.random() * finishes.length)];

    const enhancedPrompt = `Legendary trading card crest emblem, ${prompt}, ${randomStyle}, ${randomFinish}, intricate details, symmetrical design, centered composition, dark dramatic background, volumetric lighting, masterpiece quality`;

    // Optimized for speed while maintaining quality
    const payload = {
        key: apiKey,
        model_id: modelId,
        prompt: enhancedPrompt,
        negative_prompt: "text, words, letters, numbers, writing, typography, font, alphabet, signature, watermark, label, title, caption, inscription, human, person, face, body, realistic photo, blurry, low quality, ugly, deformed, amateur",
        width: "512",
        height: "512",
        samples: "1",
        num_inference_steps: "15",  // Reduced for faster generation (still good quality at 15)
        guidance_scale: 6.5,        // Lower for speed
        safety_checker: "no",
        enhance_prompt: "no",       // Skip prompt enhancement for speed
        seed: null,
        webhook: null,
        track_id: null
    };

    log(`Calling ModelsLab text2img API with model: ${modelId}...`);

    const response = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        throw new Error(`ModelsLab API error: ${response.status}`);
    }

    const result = await response.json();
    log(`ModelsLab (${modelId}) response status: ${result.status}`);

    if (result.status === 'error') {
        throw new Error(`ModelsLab error: ${result.message || result.messege || 'Unknown error'}`);
    }

    // Handle async processing with timeout
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

        // Poll with strict timeout check - up to 6 attempts with 2s interval
        for (let attempt = 0; attempt < 6; attempt++) {
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
            log(`Poll ${attempt + 1}/6: ${pollResult.status} (${Math.round(elapsed/1000)}s elapsed)`);

            if (pollResult.status === 'success' && pollResult.output && pollResult.output.length > 0) {
                log(`ModelsLab (${modelId}) completed in ${Math.round(elapsed/1000)}s`);
                return pollResult.output[0];
            }

            if (pollResult.status === 'error') {
                throw new Error(`ModelsLab polling error: ${pollResult.message}`);
            }

            // Check if we have time for another poll (2s wait + buffer)
            if (Date.now() - startTime + 3000 > maxWaitMs) {
                throw new Error('ModelsLab timeout: not enough time for next poll');
            }

            // Wait 2 seconds before next poll
            await new Promise(resolve => setTimeout(resolve, 2000));
        }

        throw new Error('ModelsLab timeout: max poll attempts reached');
    }

    // Immediate result
    if (result.status === 'success' && result.output && result.output.length > 0) {
        return result.output[0];
    }

    throw new Error('ModelsLab: No image URL in response');
}

/**
 * Generate image with 6-LAYER FALLBACK system for 100% reliability.
 * Each ModelsLab model uses a different queue, maximizing success chance.
 * Two different OpenAI approaches (fetch vs SDK) for maximum reliability.
 *
 * Fallback order:
 * 1. ModelsLab SDXL (primary, cheapest)
 * 2. OpenAI DALL-E 2 via native fetch (first backup)
 * 3. OpenAI DALL-E 2 via SDK (second backup, different approach)
 * 4. ModelsLab Realvis XL v4.0 (third backup, different queue)
 * 5. ModelsLab Juggernaut XL v8 (fourth backup, another queue)
 * 6. ModelsLab DreamShaper XL (fifth backup, last resort)
 */
async function generateImageWithFallbacks(prompt, modelsLabApiKey, openaiApiKey, log, error) {
    let imageUrl = null;
    const attempts = [];

    // ===== LAYER 1: ModelsLab SDXL (Primary) =====
    if (modelsLabApiKey) {
        log(`[1/6] Trying LAYER 1: ModelsLab SDXL (primary)...`);
        try {
            imageUrl = await generateImageWithModelsLab(prompt, modelsLabApiKey, 'sdxl', log, error, 45000);
            log(`SUCCESS: Image generated with ModelsLab SDXL`);
            return { imageUrl, provider: 'ModelsLab SDXL', attempts: [...attempts, { layer: 1, provider: 'ModelsLab SDXL', success: true }] };
        } catch (err) {
            error(`Layer 1 (ModelsLab SDXL) failed: ${err.message}`);
            attempts.push({ layer: 1, provider: 'ModelsLab SDXL', success: false, error: err.message });
        }
    }

    // ===== LAYER 2: OpenAI DALL-E 2 via Native Fetch =====
    if (openaiApiKey) {
        log(`[2/6] Trying LAYER 2: OpenAI DALL-E 2 (native fetch)...`);
        try {
            imageUrl = await callDallEWithFetch(openaiApiKey, prompt, log);
            log(`SUCCESS: Image generated with OpenAI DALL-E 2 (fetch)`);
            return { imageUrl, provider: 'OpenAI DALL-E 2 (fetch)', attempts: [...attempts, { layer: 2, provider: 'OpenAI DALL-E 2 (fetch)', success: true }] };
        } catch (err) {
            error(`Layer 2 (DALL-E 2 fetch) failed: ${err.message}`);
            attempts.push({ layer: 2, provider: 'OpenAI DALL-E 2 (fetch)', success: false, error: err.message });
        }
    }

    // ===== LAYER 3: OpenAI DALL-E 2 via SDK (Different Approach) =====
    if (openaiApiKey) {
        log(`[3/6] Trying LAYER 3: OpenAI DALL-E 2 (SDK)...`);
        try {
            imageUrl = await callDallEWithSDK(openaiApiKey, prompt, log);
            log(`SUCCESS: Image generated with OpenAI DALL-E 2 (SDK)`);
            return { imageUrl, provider: 'OpenAI DALL-E 2 (SDK)', attempts: [...attempts, { layer: 3, provider: 'OpenAI DALL-E 2 (SDK)', success: true }] };
        } catch (err) {
            error(`Layer 3 (DALL-E 2 SDK) failed: ${err.message}`);
            attempts.push({ layer: 3, provider: 'OpenAI DALL-E 2 (SDK)', success: false, error: err.message });
        }
    }

    // ===== LAYER 4: ModelsLab Realvis XL v4.0 =====
    if (modelsLabApiKey) {
        log(`[4/6] Trying LAYER 4: ModelsLab Realvis XL v4.0...`);
        try {
            imageUrl = await generateImageWithModelsLab(prompt, modelsLabApiKey, 'realvis-xl-v40', log, error, 35000);
            log(`SUCCESS: Image generated with ModelsLab Realvis XL v4.0`);
            return { imageUrl, provider: 'ModelsLab Realvis XL v4.0', attempts: [...attempts, { layer: 4, provider: 'ModelsLab Realvis XL v4.0', success: true }] };
        } catch (err) {
            error(`Layer 4 (Realvis XL v4.0) failed: ${err.message}`);
            attempts.push({ layer: 4, provider: 'ModelsLab Realvis XL v4.0', success: false, error: err.message });
        }
    }

    // ===== LAYER 5: ModelsLab Juggernaut XL v8 =====
    if (modelsLabApiKey) {
        log(`[5/6] Trying LAYER 5: ModelsLab Juggernaut XL v8...`);
        try {
            imageUrl = await generateImageWithModelsLab(prompt, modelsLabApiKey, 'juggernaut-xl-v8', log, error, 35000);
            log(`SUCCESS: Image generated with ModelsLab Juggernaut XL v8`);
            return { imageUrl, provider: 'ModelsLab Juggernaut XL v8', attempts: [...attempts, { layer: 5, provider: 'ModelsLab Juggernaut XL v8', success: true }] };
        } catch (err) {
            error(`Layer 5 (Juggernaut XL v8) failed: ${err.message}`);
            attempts.push({ layer: 5, provider: 'ModelsLab Juggernaut XL v8', success: false, error: err.message });
        }
    }

    // ===== LAYER 6: ModelsLab DreamShaper XL (Last Resort) =====
    if (modelsLabApiKey) {
        log(`[6/6] Trying LAYER 6: ModelsLab DreamShaper XL (last resort)...`);
        try {
            imageUrl = await generateImageWithModelsLab(prompt, modelsLabApiKey, 'dreamshaper-xl', log, error, 35000);
            log(`SUCCESS: Image generated with ModelsLab DreamShaper XL`);
            return { imageUrl, provider: 'ModelsLab DreamShaper XL', attempts: [...attempts, { layer: 6, provider: 'ModelsLab DreamShaper XL', success: true }] };
        } catch (err) {
            error(`Layer 6 (DreamShaper XL) failed: ${err.message}`);
            attempts.push({ layer: 6, provider: 'ModelsLab DreamShaper XL', success: false, error: err.message });
        }
    }

    // All 6 layers exhausted - this should be extremely rare
    error(`CRITICAL: All 6 image generation layers failed!`);
    return { imageUrl: null, provider: null, attempts };
}

module.exports = async function (context) {
    const { req, res, log, error } = context;

    log('='.repeat(60));
    log('GENERATE-ARCHETYPE v5 (6-layer fallback, 100% reliability)');
    log('='.repeat(60));

    // Parse request body early for warmup check
    let body;
    if (typeof req.body === 'string') {
        try {
            body = JSON.parse(req.body);
        } catch {
            body = {};
        }
    } else {
        body = req.body || {};
    }

    // Pre-warm endpoint - returns immediately, costs nothing
    // Just wakes up the function container and establishes connections
    if (body.warmup === true) {
        log('Pre-warm request received - returning immediately');
        return res.json({ success: true, warmup: true, message: 'Function warmed up' });
    }

    // Initialize clients
    const client = new Client()
        .setEndpoint(process.env.APPWRITE_FUNCTION_API_ENDPOINT)
        .setProject(process.env.APPWRITE_FUNCTION_PROJECT_ID)
        .setKey(process.env.APPWRITE_API_KEY);

    const databases = new Databases(client);
    const storage = new Storage(client);

    const databaseId = process.env.DATABASE_ID;
    const archetypesCollection = process.env.ARCHETYPES_COLLECTION_ID;
    const crestsBucket = process.env.CRESTS_BUCKET_ID;
    const modelsLabApiKey = process.env.MODELSLAB_API_KEY;
    const openaiApiKey = process.env.OPENAI_API_KEY;

    try {
        // Body already parsed above for warmup check
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

        // Step 1: Generate archetype data with GPT-4 (using native fetch)
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

        const messages = [
            { role: 'system', content: ARCHETYPE_SYSTEM_PROMPT },
            { role: 'user', content: userPrompt }
        ];

        const gptContent = await callOpenAIChatWithFetch(openaiApiKey, messages, log);

        let archetypeData;
        try {
            archetypeData = JSON.parse(gptContent);
        } catch (parseErr) {
            error(`Failed to parse GPT response: ${gptContent}`);
            return res.json({ success: false, error: 'Failed to parse archetype data' });
        }

        log(`Archetype generated: ${archetypeData.archetype}`);

        // Step 2: Generate crest image with 5-LAYER FALLBACK for 100% reliability
        let crestImageUrl = null;
        let imageProvider = null;
        let fallbackAttempts = [];

        if (archetypeData.image_prompt) {
            log(`Starting 6-layer image generation fallback system...`);
            const result = await generateImageWithFallbacks(
                archetypeData.image_prompt,
                modelsLabApiKey,
                openaiApiKey,
                log,
                error
            );
            crestImageUrl = result.imageUrl;
            imageProvider = result.provider;
            fallbackAttempts = result.attempts || [];

            if (crestImageUrl) {
                log(`Image generated successfully using: ${imageProvider}`);
            } else {
                error(`CRITICAL: All 6 image generation layers failed! Archetype saved without image.`);
                log(`Failed attempts: ${JSON.stringify(fallbackAttempts)}`);
            }
        }

        // Step 3: Save to database
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

        log(`SUCCESS: Archetype generated for ${playerName}`);
        if (imageProvider) {
            log(`Image provider used: ${imageProvider}`);
        }

        return res.json({
            success: true,
            data: {
                archetype: archetypeData.archetype,
                subArchetype: archetypeData.sub_archetype,
                playStyleSummary: archetypeData.play_style_summary,
                crestImageUrl: crestImageUrl,
                confidence: archetypeData.confidence,
                imageProvider: imageProvider  // Track which provider succeeded
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
