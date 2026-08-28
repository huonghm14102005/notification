import fs from 'fs';
import path from 'path';
import { Client } from '@notionhq/client';
import { markdownToBlocks } from '@tryfabric/martian';

let notionToken = process.env.NOTION_TOKEN;
let rawDbId = process.env.NOTION_DATABASE_ID;
const projectName = process.env.PROJECT_NAME || 'Notification Server';

if (!notionToken) {
  console.error('❌ Thiếu biến môi trường NOTION_TOKEN');
  process.exit(1);
}

function cleanId(input) {
  if (!input) return '';
  let cleaned = input.trim().replace(/^collection:\/\//, '');
  const match = cleaned.match(/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|[0-9a-fA-F]{32}/);
  return match ? match[0] : cleaned;
}

let databaseId = cleanId(rawDbId);
const notion = new Client({ auth: notionToken.trim() });

// Hàm tự động retry khi gặp Rate Limit (HTTP 429) của Notion API
async function withRetry(fn, maxRetries = 5) {
  let delay = 1500;
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await fn();
    } catch (err) {
      if (err.code === 'rate_limited' || err.status === 429 || (err.message && err.message.toLowerCase().includes('rate limited'))) {
        console.warn(`⏳ Notion Rate Limit: Đang tạm nghỉ ${delay / 1000}s trước khi thử lại... (Lần ${i + 1}/${maxRetries})`);
        await new Promise(r => setTimeout(r, delay));
        delay = Math.min(delay * 2, 10000);
      } else {
        throw err;
      }
    }
  }
  return await fn();
}

function determineDocType(filePath) {
  const normalized = filePath.replace(/\\/g, '/');
  const baseName = path.basename(filePath).toUpperCase();

  if (normalized.includes('/adr/')) return 'ADR';
  if (normalized.includes('/features/')) return 'Feature Spec';
  if (normalized.includes('/changelog/')) return 'Changelog';

  if (baseName.includes('ARCHITECTURE')) return 'Architecture';
  if (baseName.includes('CONVENTIONS')) return 'Conventions';
  if (baseName.includes('TROUBLESHOOTING')) return 'Troubleshooting';
  if (baseName.includes('DEPLOYMENT') || baseName.includes('DEPLOY')) return 'Deployment';
  if (baseName.includes('PRODUCT')) return 'Product';
  if (baseName.includes('SPECS')) return 'Specs';
  if (baseName.includes('WORKFLOW')) return 'Workflow';
  if (baseName.includes('MVP')) return 'Product';
  if (baseName.includes('ROADMAP')) return 'Roadmap';
  if (baseName.includes('PRODUCTION-READINESS')) return 'Production Readiness';
  if (baseName.includes('TARGET-DESIGN')) return 'Architecture';
  if (baseName.includes('AGENTS')) return 'Agent Rule & Skill';
  if (baseName.includes('README')) return 'Overview';

  return 'Documentation';
}

async function getMarkdownFiles(dir) {
  if (!fs.existsSync(dir)) return [];
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!['node_modules', '.git', '.next', 'dist', 'bin', 'obj'].includes(entry.name)) {
        files.push(...(await getMarkdownFiles(fullPath)));
      }
    } else if (entry.isFile() && entry.name.endsWith('.md')) {
      files.push(fullPath);
    }
  }
  return files;
}

async function findExistingPage(actualDbId, filePathKey, title, dbSchema) {
  const filePathProp = dbSchema.filePathProp;
  
  if (filePathProp) {
    try {
      const response = await withRetry(() => notion.databases.query({
        database_id: actualDbId,
        filter: {
          property: filePathProp,
          rich_text: {
            equals: filePathKey,
          },
        },
      }));
      const active = response.results.find(p => !p.archived && !p.in_trash);
      if (active) return active;
    } catch (e) {}
  }

  if (dbSchema.titleProp) {
    try {
      const response = await withRetry(() => notion.databases.query({
        database_id: actualDbId,
        filter: {
          property: dbSchema.titleProp,
          title: {
            equals: title,
          },
        },
      }));
      const active = response.results.find(p => !p.archived && !p.in_trash);
      if (active) return active;
    } catch (e) {}
  }

  return null;
}

async function resolveDatabase() {
  console.log(`🔍 Đang tự động quét Notion để tìm Database...`);
  
  // 1. Thử lấy trực tiếp bằng databaseId trước nếu có
  if (databaseId) {
    try {
      const db = await withRetry(() => notion.databases.retrieve({ database_id: databaseId }));
      console.log(`✅ Kết nối trực tiếp Database ID: ${db.id}`);
      return db;
    } catch (e) {}

    try {
      const blocks = await withRetry(() => notion.blocks.children.list({ block_id: databaseId }));
      const childDb = blocks.results.find(b => b.type === 'child_database');
      if (childDb) {
        const db = await withRetry(() => notion.databases.retrieve({ database_id: childDb.id }));
        console.log(`✅ Tìm thấy Database con trong Page: ${db.id}`);
        return db;
      }
    } catch (e) {}
  }

  // 2. Search toàn bộ workspace
  try {
    const searchRes = await withRetry(() => notion.search({}));
    console.log(`📦 Tìm thấy ${searchRes.results.length} đối tượng trong Notion mà bot có quyền.`);

    // Ưu tiên database có tên Knowledge Hub hoặc Knowledge
    const db = searchRes.results.find(item => {
      const isDb = item.object === 'database' || item.object === 'data_source';
      let title = '';
      if (item.title && Array.isArray(item.title) && item.title[0]) {
        title = item.title[0].plain_text || item.title[0].text?.content || '';
      }
      return isDb && (title.toLowerCase().includes('knowledge') || title.toLowerCase().includes('hub'));
    }) || searchRes.results.find(item => item.object === 'database' || item.object === 'data_source');

    if (db) {
      let dbTitle = 'Knowledge Database';
      if (db.title && Array.isArray(db.title) && db.title[0]) {
        dbTitle = db.title[0].plain_text || db.title[0].text?.content || dbTitle;
      }
      console.log(`✅ Đã tự động kết nối Database: "${dbTitle}" (ID: ${db.id})`);
      return db;
    }

    // Nếu chỉ có pages, thử tìm child database trong các pages
    for (const item of searchRes.results) {
      if (item.object === 'page') {
        try {
          const blocks = await withRetry(() => notion.blocks.children.list({ block_id: item.id }));
          const childDb = blocks.results.find(b => b.type === 'child_database');
          if (childDb) {
            const db = await withRetry(() => notion.databases.retrieve({ database_id: childDb.id }));
            console.log(`✅ Tìm thấy Database con trong Page (${item.id}): ${db.id}`);
            return db;
          }
        } catch (e) {}
      }
    }
  } catch (err) {
    console.warn(`⚠️ Search warning:`, err.message);
  }

  throw new Error(`Không tìm thấy Database nào được chia sẻ với Integration. Hãy kiểm tra lại nút Add Connection trên Notion.`);
}

function chunkArray(array, size) {
  const result = [];
  for (let i = 0; i < array.length; i += size) {
    result.push(array.slice(i, i + size));
  }
  return result;
}

async function syncFile(actualDbId, filePath, dbSchema) {
  const relativePath = path.relative(process.cwd(), filePath).replace(/\\/g, '/');
  const content = fs.readFileSync(filePath, 'utf-8');

  const firstLineMatch = content.match(/^#\s+(.+)$/m);
  const title = firstLineMatch ? firstLineMatch[1].trim() : path.basename(filePath, '.md');
  const docType = determineDocType(relativePath);

  let blocks = [];
  try {
    blocks = markdownToBlocks(content);
  } catch (err) {
    console.warn(`⚠️ Lỗi parse markdown cho ${relativePath}, dùng plain text:`, err.message);
    blocks = [{
      object: 'block',
      type: 'paragraph',
      paragraph: {
        rich_text: [{ type: 'text', text: { content: content.slice(0, 2000) } }]
      }
    }];
  }

  const blockChunks = chunkArray(blocks, 100);
  const initialBlocks = blockChunks.length > 0 ? blockChunks[0] : [];

  const propertiesPayload = {};

  if (dbSchema.titleProp) {
    propertiesPayload[dbSchema.titleProp] = {
      title: [{ text: { content: title } }],
    };
  }

  if (dbSchema.projectProp && dbSchema.raw[dbSchema.projectProp]) {
    const propType = dbSchema.raw[dbSchema.projectProp].type;
    if (propType === 'select') {
      propertiesPayload[dbSchema.projectProp] = { select: { name: projectName } };
    } else if (propType === 'rich_text') {
      propertiesPayload[dbSchema.projectProp] = { rich_text: [{ text: { content: projectName } }] };
    }
  }

  if (dbSchema.typeProp && dbSchema.raw[dbSchema.typeProp]) {
    const propType = dbSchema.raw[dbSchema.typeProp].type;
    if (propType === 'select') {
      propertiesPayload[dbSchema.typeProp] = { select: { name: docType } };
    } else if (propType === 'rich_text') {
      propertiesPayload[dbSchema.typeProp] = { rich_text: [{ text: { content: docType } }] };
    }
  }

  if (dbSchema.filePathProp && dbSchema.raw[dbSchema.filePathProp]) {
    propertiesPayload[dbSchema.filePathProp] = {
      rich_text: [{ text: { content: relativePath } }],
    };
  }

  if (dbSchema.dateProp) {
    propertiesPayload[dbSchema.dateProp] = {
      date: { start: new Date().toISOString() },
    };
  }

  let existingPage = await findExistingPage(actualDbId, relativePath, title, dbSchema);

  if (existingPage) {
    console.log(`🔄 [UPDATE] ${relativePath} -> "${title}" (${docType})`);
    try {
      await withRetry(() => notion.pages.update({
        page_id: existingPage.id,
        archived: false,
        properties: propertiesPayload,
      }));

      const currentBlocks = await withRetry(() => notion.blocks.children.list({ block_id: existingPage.id }));
      for (const block of currentBlocks.results) {
        try {
          await withRetry(() => notion.blocks.delete({ block_id: block.id }));
        } catch (e) {}
      }

      for (const chunk of blockChunks) {
        if (chunk.length > 0) {
          await withRetry(() => notion.blocks.children.append({
            block_id: existingPage.id,
            children: chunk,
          }));
        }
      }
      return;
    } catch (err) {
      console.warn(`⚠️ Update không thành công (${err.message}), tạo mới thay thế...`);
    }
  }

  console.log(`✨ [CREATE] ${relativePath} -> "${title}" (${docType})`);
  const newPage = await withRetry(() => notion.pages.create({
    parent: { database_id: actualDbId },
    properties: propertiesPayload,
    children: initialBlocks,
  }));

  for (let i = 1; i < blockChunks.length; i++) {
    await withRetry(() => notion.blocks.children.append({
      page_id: newPage.id,
      children: blockChunks[i],
    }));
  }
}

async function main() {
  console.log(`====================================================`);
  console.log(`🚀 Bắt đầu đồng bộ Docs lên Notion cho: ${projectName}`);
  console.log(`====================================================`);

  let db;
  try {
    db = await resolveDatabase();
  } catch (err) {
    console.error(`❌ Không thể truy cập Notion Database:`, err.message);
    process.exit(1);
  }

  const actualDbId = db.id;
  const properties = db.properties;

  let titleProp = Object.keys(properties).find(k => properties[k].type === 'title') || 'Title';
  let projectProp = Object.keys(properties).find(k => k.toLowerCase() === 'project' || k.toLowerCase() === 'dự án');
  let typeProp = Object.keys(properties).find(k => k.toLowerCase() === 'type' || k.toLowerCase() === 'category' || k.toLowerCase() === 'loại');
  let filePathProp = Object.keys(properties).find(k => k.toLowerCase() === 'filepath' || k.toLowerCase() === 'file path' || k.toLowerCase() === 'file');
  let dateProp = Object.keys(properties).find(k => properties[k].type === 'date' || k.toLowerCase().includes('updated'));

  const dbSchema = { titleProp, projectProp, typeProp, filePathProp, dateProp, raw: properties };
  console.log(`📋 Cấu trúc Database nhận diện được: Title="${titleProp}", Project="${projectProp || 'N/A'}", Type="${typeProp || 'N/A'}"`);

  const docsDir = path.resolve(process.cwd(), 'docs');
  const files = await getMarkdownFiles(docsDir);

  if (fs.existsSync('README.md')) files.push(path.resolve('README.md'));
  if (fs.existsSync('AGENTS.md')) files.push(path.resolve('AGENTS.md'));

  console.log(`📂 Tìm thấy ${files.length} file tài liệu cần đồng bộ.`);

  for (let i = 0; i < files.length; i++) {
    const file = files[i];
    try {
      await syncFile(actualDbId, file, dbSchema);
      await new Promise(r => setTimeout(r, 400));
    } catch (err) {
      console.error(`❌ Lỗi khi đồng bộ file ${file}:`, err.message);
    }
  }

  console.log(`\n🎉 Hoàn tất đồng bộ toàn bộ tài liệu cho ${projectName}!`);
}

main();
