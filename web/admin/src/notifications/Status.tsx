const labels:Record<string,string>={accepted:'Đã tiếp nhận',processing:'Đang gửi',delivered:'Đã gửi',partially_delivered:'Gửi một phần',failed:'Thất bại',cancelled:'Đã hủy',pending:'Chờ gửi',sending:'Đang gửi'};
export const Status=({value}:{value:string})=><span className={`status s-${value}`}><span aria-hidden>●</span>{labels[value]??value}</span>;
export const Time=({value}:{value?:string})=>value?<time title={value} dateTime={value}>{new Intl.DateTimeFormat('vi-VN',{dateStyle:'medium',timeStyle:'short'}).format(new Date(value))}</time>:<>—</>;
