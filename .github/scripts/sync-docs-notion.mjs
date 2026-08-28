import fs from 'fs';
import path from 'path';
import { Client } from '@notionhq/client';
import { markdownToBlocks } from '@tryfabric/martian';

const notionToken = process.env.NOTION_TOKEN;
const databaseId = process.env.NOTION_DATABASE_ID;
const projectName = process.env.PROJECT_NAME || 'Notification Server';

if (!notionToken || !databaseId) {
  console.error('❌ Thiếu biến môi trường NOTION_TOKEN hoặc NOTION_DATABASE_ID');
  process.exit(1);
}

const notion = new Client({ auth: notionToken });

// Hàm xác định loại tài liệu (Type/Category)
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

// Quét toàn bộ file markdown
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

// Tìm page đã tồn tại trên Notion (bằng FilePath hoặc Title)
async function findExistingPage(filePathKey, title, dbSchema) {
  const filePathProp = dbSchema.filePathProp;
  
  if (filePathProp) {
    try {
      const response = await notion.databases.query({
        database_id: databaseId,
        filter: {
          property: filePathProp,
          rich_text: {
            equals: filePathKey,
          },
        },
      });
      if (response.results.length > 0) return response.results[0];
    } catch (e) {
      console.warn(`⚠️ Filter theo FilePath không thành công, thử filter theo Title...`);
    }
  }

  // Fallback filter theo title
  if (dbSchema.titleProp) {
    try {
      const response = await notion.databases.query({
        database_id: databaseId,
        filter: {
          property: dbSchema.titleProp,
          title: {
            equals: title,
          },
        },
      });
      if (response.results.length > 0) return response.results[0];
    } catch (e) {
      // Bỏ qua nếu query lỗi
    }
  }

  return null;
}

// Lấy thông tin schema của Database để tự động thích ứng với tên cột
async function inspectDatabaseSchema() {
  const db = await notion.databases.retrieve({ database_id: databaseId });
  const properties = db.properties;

  let titleProp = Object.keys(properties).find(k => properties[k].type === 'title') || 'Title';
  let projectProp = Object.keys(properties).find(k => k.toLowerCase() === 'project' || k.toLowerCase() === 'dự án');
  let typeProp = Object.keys(properties).find(k => k.toLowerCase() === 'type' || k.toLowerCase() === 'category' || k.toLowerCase() === 'loại');
  let filePathProp = Object.keys(properties).find(k => k.toLowerCase() === 'filepath' || k.toLowerCase() === 'file path' || k.toLowerCase() === 'file');
  let dateProp = Object.keys(properties).find(k => properties[k].type === 'date' || k.toLowerCase().includes('updated'));

  return { titleProp, projectProp, typeProp, filePathProp, dateProp, raw: properties };
}

// Chia nhỏ mảng blocks thành từng nhóm 100 blocks (giới hạn của Notion API)
function chunkArray(array, size) {
  const result = [];
  for (let i = 0; i < array.length; i += size) {
    result.push(array.slice(i, i + size));
  }
  return result;
}

async function syncFile(filePath, dbSchema) {
  const relativePath = path.relative(process.cwd(), filePath).replace(/\\/g, '/');
  const content = fs.readFileSync(filePath, 'utf-8');

  // Lấy dòng heading # đầu tiên làm Title
  const firstLineMatch = content.match(/^#\s+(.+)$/m);
  const title = firstLineMatch ? firstLineMatch[1].trim() : path.basename(filePath, '.md');
  const docType = determineDocType(relativePath);

  // Convert markdown sang Notion blocks
  let blocks = [];
  try {
    blocks = markdownToBlocks(content);
  } catch (err) {
    console.warn(`⚠️ Lỗi parse markdown cho ${relativePath}, dùng plain text thay thế:`, err.message);
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

  // Tạo payload properties
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

  const existingPage = await findExistingPage(relativePath, title, dbSchema);

  if (existingPage) {
    console.log(`🔄 [UPDATE] ${relativePath} -> "${title}" (${docType})`);
    
    // Cập nhật thuộc tính
    await notion.pages.update({
      page_id: existingPage.id,
      properties: propertiesPayload,
    });

    // Xóa block cũ để thay mới
    const currentBlocks = await notion.blocks.children.list({ block_id: existingPage.id });
    for (const block of currentBlocks.results) {
      await notion.blocks.delete({ block_id: block.id });
    }

    // Ghi lại blocks
    for (const chunk of blockChunks) {
      if (chunk.length > 0) {
        await notion.blocks.children.append({
          block_id: existingPage.id,
          children: chunk,
        });
      }
    }
  } else {
    console.log(`✨ [CREATE] ${relativePath} -> "${title}" (${docType})`);
    const newPage = await notion.pages.create({
      parent: { database_id: databaseId },
      properties: propertiesPayload,
      children: initialBlocks,
    });

    // Nếu có hơn 100 blocks, append các phần còn lại
    for (let i = 1; i < blockChunks.length; i++) {
      await notion.blocks.children.append({
        page_id: newPage.id,
        children: blockChunks[i],
      });
    }
  }
}

async function main() {
  console.log(`====================================================`);
  console.log(`🚀 Bắt đầu đồng bộ Docs lên Notion cho: ${projectName}`);
  console.log(`====================================================`);

  let dbSchema;
  try {
    dbSchema = await inspectDatabaseSchema();
    console.log(`📋 Nhận diện cấu trúc Notion DB thành công!`);
  } catch (err) {
    console.error(`❌ Không thể truy cập Notion Database (kiểm tra token và quyền share):`, err.message);
    process.exit(1);
  }

  const docsDir = path.resolve(process.cwd(), 'docs');
  const files = await getMarkdownFiles(docsDir);

  if (fs.existsSync('README.md')) files.push(path.resolve('README.md'));
  if (fs.existsSync('AGENTS.md')) files.push(path.resolve('AGENTS.md'));

  console.log(`📂 Tìm thấy ${files.length} file tài liệu cần đồng bộ.`);

  for (const file of files) {
    try {
      await syncFile(file, dbSchema);
    } catch (err) {
      console.error(`❌ Lỗi khi đồng bộ file ${file}:`, err.message);
    }
  }

  console.log(`\n🎉 Hoàn tất đồng bộ toàn bộ tài liệu cho ${projectName}!`);
}

main();
