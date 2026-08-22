export type Tokens={accessToken:string;refreshToken:string;admin:{id:string;tenantId:string;role:string}};
export type Delivery={id:string;channel:string;target:string;targetRef?:string;status:string;attemptCount:number;errorCode?:string};
export type NotificationItem={id:string;sourceDeviceId?:string;apiKeyId?:string;producerName:string;status:string;createdAt:string;updatedAt:string;completedAt?:string;deliveries:Delivery[]};
export type Page={items:NotificationItem[];nextCursor:string|null};
export type Attempt={attemptNo:number;result:string;startedAt:string;finishedAt:string;errorCode?:string;errorMessage?:string;providerMessageId?:string};
export type Detail={id:string;tenantId:string;producerName:string;senderKey:string;status:string;recipientEmail:string;recipientRef?:string;subject?:string;body?:string;createdAt:string;sentAt?:string;updatedAt:string;failureReason?:string;deliveryAttempts:Attempt[]};
export class ApiError extends Error{constructor(public status:number,public code:string){super(code)}}
